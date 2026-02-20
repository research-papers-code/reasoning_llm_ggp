import os
import json
import glob
import time
import re
import argparse
from typing import List, Optional, Union, Type, TypeVar
from pydantic import BaseModel, Field, ValidationError
from dotenv import load_dotenv

# Third-party libraries
from google import genai
from openai import OpenAI
from google.genai.types import GenerateContentConfig, HttpOptions
from cerebras.cloud.sdk import Cerebras

# --- 1. Prompts ---

PROMPT_EVAL_NEXT_STATE = """
You are a game logic expert. Your task is to predict the next game state.

Here is the game definition in GDL (Game Description Language). GDL was used in General Game Playing competition. Ignore the Init part; the current state will be provided later:
--- GAME DEFINITION ---
{game_definition}
-----------------------

Here is the current game state:
--- GAME STATE ---
{game_state}
------------------

The following move is being executed:
--- MOVE ---
{move}
------------

What will be the **exact** game state after this move?
Respond **only** in JSON format using the following fields:
- "llm_state": string, the complete new game state in the same format as the input state, each fact separated by new line symbol

Do not add any explanations, comments, or markdown formatting.
Your response must start with {{.
"""

PROMPT_EVAL_LEGAL_MOVES = """
You are a game logic expert. Your task is to determine all legal moves available for all players in the current state.

Here is the game definition in GDL (Game Description Language):
--- GAME DEFINITION ---
{game_definition}
-----------------------

Here is the current game state:
--- GAME STATE ---
{game_state}
------------------

List **all** legal moves for this state based on the GDL rules.
Respond **only** in JSON format using the following field:
- "llm_legal_moves": string which contains all possible valid GDL moves (role + action) separated by new line symbol, each move should be in round brackets

Do not add any explanations, comments, or markdown formatting. Don't put GDL syntactic elements like "legal", "does" in the moves.
Your response must start with {{.
"""

PROMPT_EVAL_MULTI_STEP = """
You are a game logic expert. Your task is to predict the game state after a specific sequence of moves.

Here is the game definition in GDL (Game Description Language). 
The game starts from the initial state defined in this GDL (look for 'init' relations).
--- GAME DEFINITION ---
{game_definition}
-----------------------

Starting from the initial state defined above, apply the following sequence of {n} moves in order:
--- MOVE SEQUENCE ---
{move_sequence}
---------------------

What will be the **exact** game state after these {n} moves have been executed?
Respond **only** in JSON format using the following fields:
- "llm_state": string, the complete new game state in GDL format, each fact separated by new line symbol.

Do not add any explanations, comments, or markdown formatting.
Your response must start with {{.
"""

PROMPT_EVAL_MULTI_STEP_GEN = """
You are a game logic expert. Your task is to play the game for {n} steps and predict the resulting state.

Here is the game definition in GDL (Game Description Language).
The game starts from the initial state defined in this GDL (look for 'init' relations).
--- GAME DEFINITION ---
{game_definition}
-----------------------

**Task:**
1. Starting from the initial state, generate a valid sequence of {n} moves.
2. Calculate the exact game state after these {n} moves.

**Move Format:**
Your generated moves must follow this specific string format (example from this game):
{move_example}

**Output:**
Respond **only** in JSON format using the following structure:
{{
    "moves": [
        {{ "step": "0", "joint_move": "..." }},
        {{ "step": "1", "joint_move": "..." }}
    ],
    "llm_state": "string containing the complete new game state in GDL format"
}}

Do not add any explanations, comments, or markdown formatting.
Your response must start with {{.
"""

# --- 2. Data Structures ---

# Generic TypeVar for response parsing
T = TypeVar("T", bound=BaseModel)

# -- Input Models --

class MoveStep(BaseModel):
    """Represents a single step in the multi-step input format."""
    step: str
    joint_move: str

class EvalSample(BaseModel):
    """Structure of a single sample from the standard INPUT file (next_state/legal_moves)."""
    game_state: str
    move: Optional[str] = None
    next_state: Optional[str] = None
    legal_moves: Optional[str] = None

class MultiStepInputSample(BaseModel):
    """Structure of a sample from the multi-step INPUT file."""
    moves: List[MoveStep]

class EvalData(BaseModel):
    """Structure of the input JSON file. Can contain either standard samples or multi-step samples."""
    game_name: str
    # Polymorphic list: parses based on structure
    samples: List[Union[EvalSample, MultiStepInputSample]]

# -- Experiment 1 Output Models (Next State) --
class LLMNextStateResponse(BaseModel):
    llm_state: str = Field(..., description="The predicted next game state.")

class OutputSampleNextState(BaseModel):
    game_state: str
    move: str
    next_state: str
    llm_state: str

# -- Experiment 2 Output Models (Legal Moves) --
class LLMLegalMovesResponse(BaseModel):
    llm_legal_moves: str = Field(..., description="List of available moves in a given state separated by new line symbol.")

class OutputSampleLegalMoves(BaseModel):
    game_state: str
    legal_moves: str  # Ground truth
    llm_legal_moves: str # Prediction

# -- Experiment 3 Output Models (Multi Step Prediction) --
class OutputSampleMultiStep(BaseModel):
    moves: List[MoveStep]
    llm_state: str

# -- Experiment 4 Output Models (Multi Step Generation) --
class LLMMultiStepGenResponse(BaseModel):
    moves: List[MoveStep] = Field(..., description="The sequence of moves generated by LLM.")
    llm_state: str = Field(..., description="The predicted game state after the generated moves.")

class OutputSampleMultiStepGen(BaseModel):
    moves: List[MoveStep] 
    llm_state: str

# -- Final Output Wrapper --
class OutputData(BaseModel):
    game_name: str
    llm_model: str
    experiment_type: str
    samples: List[Union[OutputSampleNextState, OutputSampleLegalMoves, OutputSampleMultiStep, OutputSampleMultiStepGen]] = Field(default_factory=list)


# --- 3. Player Implementations ---

class BaseGamePlayer:
    """Base interface for game players with shared parsing logic."""
    def __init__(self, model_name: str, api_key: str):
        self.model_name = model_name
        self.api_key = api_key

    def _clean_response(self, text: str) -> str:
        """Removes markdown code fences and whitespace."""
        result = re.sub(r"^```(?:json)?|```$", "", text.strip(), flags=re.MULTILINE).strip()
        return re.sub(r'\}(\s*)\}(\s*)$', r'}\1', result, flags=re.DOTALL)

    def _generate_json(self, prompt: str, response_model: Type[T]) -> T:
        """Abstract method to generate and parse JSON. To be implemented by subclasses."""
        raise NotImplementedError

    # -- Specific Experiment Methods --

    def evaluate_next_state(self, game_definition: str, game_state: str, move: str) -> LLMNextStateResponse:
        prompt = PROMPT_EVAL_NEXT_STATE.format(
            game_definition=game_definition,
            game_state=game_state,
            move=move
        )
        return self._generate_json(prompt, LLMNextStateResponse)

    def evaluate_legal_moves(self, game_definition: str, game_state: str) -> LLMLegalMovesResponse:
        prompt = PROMPT_EVAL_LEGAL_MOVES.format(
            game_definition=game_definition,
            game_state=game_state
        )
        return self._generate_json(prompt, LLMLegalMovesResponse)
    
    def evaluate_multi_step(self, game_definition: str, moves: List[MoveStep]) -> LLMNextStateResponse:
        move_sequence_str = ""
        for m in moves:
            move_sequence_str += f"Step {m.step}:\n{m.joint_move}\n\n"
        
        prompt = PROMPT_EVAL_MULTI_STEP.format(
            game_definition=game_definition,
            n=len(moves),
            move_sequence=move_sequence_str.strip()
        )
        return self._generate_json(prompt, LLMNextStateResponse)

    def evaluate_multi_step_generation(self, game_definition: str, n: int, example_move: str) -> LLMMultiStepGenResponse:
        prompt = PROMPT_EVAL_MULTI_STEP_GEN.format(
            game_definition=game_definition,
            n=n,
            move_example=example_move
        )
        return self._generate_json(prompt, LLMMultiStepGenResponse)


class GeminiGamePlayer(BaseGamePlayer):
    """Implementation using Google Gemini API."""
    def __init__(self, model_name: str, api_key: str):
        super().__init__(model_name, api_key)
        self.client = genai.Client(api_key=self.api_key, http_options=HttpOptions(timeout=60*2*1000))

    def _generate_json(self, prompt: str, response_model: Type[T]) -> T:
        for attempt in range(5):
            try:
                response = self.client.models.generate_content(
                    model=self.model_name,
                    contents=prompt,
                    config=GenerateContentConfig(temperature=0.2)
                )
                clean_text = self._clean_response(response.text)
                return response_model.model_validate_json(clean_text)
            except Exception as e:
                print(f"❌ Gemini attempt {attempt+1} failed: {e}")
                time.sleep(1)
        return ""


class CerebrasGamePlayer(BaseGamePlayer):
    """Implementation using Cerebras API with automatic key rotation on quota limits."""
    def __init__(self, model_name: str, api_keys: List[str]):
        # Base class expects a single key, we pass the first one, but we manage the list here
        super().__init__(model_name, api_keys[0])
        self.api_keys = api_keys
        self.current_key_index = 0
        self._init_client()

    def _init_client(self):
        """Initializes the Cerebras client with the current active key."""
        current_key = self.api_keys[self.current_key_index]
        print(f"🔑 [Cerebras] Using API Key index: {self.current_key_index}/{len(self.api_keys) - 1}")
        self.client = Cerebras(api_key=current_key)

    def _rotate_key(self):
        """Switches to the next API key in the list."""
        if len(self.api_keys) <= 1:
            print("⚠️ Only one API key provided. Cannot rotate.")
            return

        self.current_key_index = (self.current_key_index + 1) % len(self.api_keys)
        print(f"🔄 [Cerebras] Quota limit reached. Switching to key index {self.current_key_index}...")
        self._init_client()

    def _generate_json(self, prompt: str, response_model: Type[T]) -> T:
        attempt = 0
        max_attempts = 3
        
        while attempt < max_attempts:
            try:
                response = self.client.chat.completions.create(
                    messages=[{"role": "system", "content": prompt}],
                    model=self.model_name,
                    stream=False,
                    max_completion_tokens=28192,
                    temperature=0.2,
                    top_p=0.95
                )
                clean_text = self._clean_response(response.choices[0].message.content)
                if not clean_text:
                    attempt += 1
                    continue
                return response_model.model_validate_json(clean_text)
            
            except Exception as e:
                error_str = str(e).lower()
                
                # Check for Quota/Rate Limit Error (429)
                if "429" in str(e) or "quota" in error_str or "too many tokens" in error_str:
                    print(f"🛑 Quota exceeded on current key. Error: {e}")
                    self._rotate_key()
                    # We do NOT increment 'attempt' here, so we try again immediately with the new key
                    time.sleep(1) 
                    continue
                
                print(f"❌ Cerebras attempt {attempt+1} failed: {e}")
                attempt += 1
                time.sleep(1)
                
        return ""


class NvidiaGamePlayer(BaseGamePlayer):
    """Implementation using NVIDIA API (via OpenAI client)."""
    def __init__(self, model_name: str, api_key: str):
        super().__init__(model_name, api_key)
        self.client = OpenAI(
            base_url="https://integrate.api.nvidia.com/v1",
            api_key=self.api_key
        )

    def _generate_json(self, prompt: str, response_model: Type[T]) -> T:
        for attempt in range(5):
            try:
                response = self.client.chat.completions.create(
                    messages=[
                        {"role": "system", "content": prompt}
                    ],
                    model="openai/" + self.model_name,
                    stream=False,
                    max_tokens=4096,
                    temperature=0.2,
                    top_p=0.8
                )
                raw_text = response.choices[0].message.content
                clean_text = self._clean_response(raw_text)
                return response_model.model_validate_json(clean_text)
            except Exception as e:
                print(f"❌ Nvidia attempt {attempt+1} failed: {e}")
                time.sleep(1)
        return ""

# --- 4. Core Logic ---

def process_game_file(
    gdl_file_path: str,
    samples_file_path: str,
    player: BaseGamePlayer,
    output_dir: str,
    max_samples: int,
    experiment_type: str,
    n_moves: int = None
):
    """
    Runs the LLM evaluation for a single game file based on experiment type.
    """
    print(f"🔬 Starting data collection ({experiment_type}) for: {os.path.basename(samples_file_path)}")
    experiment_id = time.strftime("%Y-%m-%d_%H%M%S")

    # Load GDL Definition
    with open(gdl_file_path, 'r') as f:
        gdl_game_definition = f.read()

    # Load Samples
    try:
        with open(samples_file_path, 'r') as f:
            samples_data_raw = json.load(f)
        eval_data = EvalData.model_validate(samples_data_raw)
    except Exception as e:
        print(f"❌ Error parsing samples file {samples_file_path}: {e}")
        return

    print(f"Found {len(eval_data.samples)} samples. Processing max {max_samples}...")

    output_samples_list = []
    
    # Processing Loop
    for i, sample in enumerate(eval_data.samples):
        if len(output_samples_list) >= max_samples:
            break
            
        print(f"\n--- Processing Sample {i + 1} / {min(len(eval_data.samples), max_samples)} ---")
        try:
            # --- EXPERIMENT 1: NEXT STATE ---
            if "next_state" in experiment_type:
                if not isinstance(sample, EvalSample): 
                    print("Skipping sample: format mismatch for next_state")
                    continue
                    
                llm_response = player.evaluate_next_state(
                    game_definition=gdl_game_definition,
                    game_state=sample.game_state,
                    move=sample.move
                )
                output_sample = OutputSampleNextState(
                    game_state=sample.game_state,
                    move=sample.move,
                    next_state=sample.next_state,
                    llm_state=llm_response.llm_state
                )

            # --- EXPERIMENT 2: LEGAL MOVES ---
            elif "legal_moves" in experiment_type:
                if not isinstance(sample, EvalSample): 
                    print("Skipping sample: format mismatch for legal_moves")
                    continue
                
                if not sample.legal_moves:
                    raise ValueError("Input sample missing 'legal_moves' field.")
                    
                llm_response = player.evaluate_legal_moves(
                    game_definition=gdl_game_definition,
                    game_state=sample.game_state
                )
                output_sample = OutputSampleLegalMoves(
                    game_state=sample.game_state,
                    legal_moves=sample.legal_moves,
                    llm_legal_moves=llm_response.llm_legal_moves
                )

            # --- EXPERIMENT 3: MULTI STEP PREDICTION ---
            elif "multi_step_prediction" in experiment_type:
                if not isinstance(sample, MultiStepInputSample):
                    print("Skipping sample: format mismatch for multi_step_prediction")
                    continue
                
                if not n_moves:
                    raise ValueError("n_moves parameter is required for multi_step_prediction")

                current_moves = sample.moves[:n_moves]
                if len(current_moves) < n_moves:
                    print(f"⚠️ Warning: Sample has fewer moves ({len(current_moves)}) than requested N={n_moves}. Using available.")

                llm_response = player.evaluate_multi_step(
                    game_definition=gdl_game_definition,
                    moves=current_moves
                )
                output_sample = OutputSampleMultiStep(
                    moves=current_moves,
                    llm_state=llm_response.llm_state
                )
            
            # --- EXPERIMENT 4: MULTI STEP GENERATION ---
            elif "multi_step_generation" in experiment_type:
                if not isinstance(sample, MultiStepInputSample):
                    print("Skipping sample: format mismatch for multi_step_generation")
                    continue

                if not n_moves:
                    raise ValueError("n_moves parameter is required for multi_step_generation")

                if not sample.moves:
                    print("Skipping sample: No moves available to use as template.")
                    continue
                    
                example_joint_move = sample.moves[0].joint_move

                llm_response = player.evaluate_multi_step_generation(
                    game_definition=gdl_game_definition,
                    n=n_moves,
                    example_move=example_joint_move
                )

                output_sample = OutputSampleMultiStepGen(
                    moves=llm_response.moves,
                    llm_state=llm_response.llm_state
                )

            else:
                print(f"Unknown experiment type: {experiment_type}")
                return

            output_samples_list.append(output_sample)
            print(f"✅ Sample {i + 1} processed.")
            time.sleep(0.1) # Rate limit safety

        except Exception as e:
            print(f"❌ Sample {i + 1} failed: {e}")
            continue

    # Save Results
    print(f"\nSuccessfully processed: {len(output_samples_list)} samples.")
    
    if len(output_samples_list) < max_samples:
        print("⚠️ No samples collected. Skipping save.")
        return

    output_data = OutputData(
        game_name=eval_data.game_name,
        llm_model=player.model_name,
        experiment_type=experiment_type,
        samples=output_samples_list
    )

    os.makedirs(output_dir, exist_ok=True)
    output_filename = os.path.join(output_dir, f"output_{eval_data.game_name}_{experiment_id}.json")
    
    try:
        with open(output_filename, 'w') as f:
            f.write(output_data.model_dump_json(indent=4))
        print(f"💾 Saved to {output_filename}")
    except Exception as e:
        print(f"❌ Failed to save JSON: {e}")


# --- 5. Main Execution ---

def main():
    load_dotenv()
    
    parser = argparse.ArgumentParser(description="Run GDL Game State Evaluation with LLMs")
    
    parser.add_argument("--experiment", type=str, 
                        choices=["next_state", "legal_moves", "multi_step_prediction", "multi_step_generation"], 
                        default="next_state", 
                        help="Choose experiment variant")
    
    parser.add_argument("--provider", type=str, choices=["gemini", "cerebras", "nvidia"], default="cerebras", help="LLM Provider")
    parser.add_argument("--model", type=str, required=True, help="Model name")
    parser.add_argument("--api_key", type=str, help="API Key (optional if set in env vars)")
    
    parser.add_argument("--samples_dir", type=str, required=True, help="Directory containing input JSON samples")
    parser.add_argument("--gdl_dir", type=str, required=True, help="Directory containing GDL (.kif/.gdl) files")
    
    parser.add_argument("--max_samples", type=int, default=25, help="Max samples to process per game")
    parser.add_argument("--reverse_order", action="store_true", help="Process files in reverse alphabetical order")
    parser.add_argument("--n_moves", type=int, help="Number of moves to predict/generate")

    args = parser.parse_args()

    # Validation
    if "multi_step" in args.experiment and not args.n_moves:
        parser.error(f"--n_moves is required when experiment is '{args.experiment}'")

    TARGET_GAMES = {
        "mummymaze2p", "wallmaze", "platformJumpers", "pacman3p", "snake_2009_big",
        "bomberman2p_InvertedRoles", "bomberman2p", "battlebrushes", "checkers",
        "checkers-mustjump", "cittaceot", "rubikscube", "god", "beatMania", "farmers",
        "qyshinsu", "snakeAssemblit", "rendezvous_asteroids", "ticTacToeLargeSuicide",
        "ticTacToeLarge", "dotsAndBoxesSuicide", "buttons", "dotsAndBoxes",
        "chineseCheckers3", "pawnWhopping", "connectfour",
        "connectFourSuicide", "checkersTiny", "othello-comp2007", "othellosuicide",
        "chess", "checkersSmall", "fighter", "1reversi2", "checkers-newgoals"
    }

    # 2. Setup Player
    player = None
    
    if args.provider == "gemini":
        api_key = args.api_key or os.getenv("GOOGLE_API_KEY")
        if not api_key: raise EnvironmentError("Missing GOOGLE_API_KEY")
        player = GeminiGamePlayer(model_name=args.model, api_key=api_key)
        
    elif args.provider == "cerebras":
        # Check for Multiple Keys First
        api_keys_str = os.getenv("CEREBRAS_API_KEYS")
        if api_keys_str:
            api_keys = [k.strip() for k in api_keys_str.split(",") if k.strip()]
        else:
            # Fallback to single key
            single_key = args.api_key or os.getenv("CEREBRAS_API_KEY")
            if single_key:
                api_keys = [single_key]
            else:
                raise EnvironmentError("Missing CEREBRAS_API_KEYS or CEREBRAS_API_KEY")
        
        player = CerebrasGamePlayer(model_name=args.model, api_keys=api_keys)
        
    elif args.provider == "nvidia":
        api_key = args.api_key or os.getenv("NVIDIA_API_KEY") # Assuming env var name
        if not api_key: raise EnvironmentError("Missing NVIDIA_API_KEY")
        player = NvidiaGamePlayer(model_name=args.model, api_key=api_key)

    # 3. File Discovery
    if not os.path.exists(args.samples_dir):
        raise FileNotFoundError(f"Samples directory not found: {args.samples_dir}")
    
    sample_files = [f for f in os.listdir(args.samples_dir) if f.endswith('.json')]
    sample_files.sort(reverse=args.reverse_order)
    
    print(f"🔥 Starting Evaluation: {args.experiment.upper()}")
    if "multi_step" in args.experiment:
        print(f"   Steps (N): {args.n_moves}")
    print(f"   Provider: {args.provider.upper()}, Model: {args.model}")
    print(f"📂 Found {len(sample_files)} sample files in {args.samples_dir}")

    # 4. Processing Loop
    for filename in sample_files:
        samples_full_path = os.path.join(args.samples_dir, filename)

        try:
            with open(samples_full_path, 'r') as f:
                content = json.load(f)
                game_name = content.get("game_name")
            
            if not game_name or (TARGET_GAMES and game_name not in TARGET_GAMES):
                continue
        except Exception as e:
            print(f"⚠️ Skipping {filename}: Read error ({e})")
            continue

        suffix = f"_n{args.n_moves}" if args.n_moves else ""
        output_dir = f"{'/'.join(args.samples_dir.split('/')[:-2])}/{args.experiment}{suffix}/{args.model}/"
        existing_pattern = os.path.join(output_dir, f"output_{game_name}_2025*.json")
        
        if glob.glob(existing_pattern):
            print(f"⏩ Skipping {game_name}: Output file already exists.")
            continue

        # Find matching GDL file
        gdl_path = os.path.join(args.gdl_dir, f"{game_name}.kif")
        if not os.path.exists(gdl_path):
            gdl_path = os.path.join(args.gdl_dir, f"{game_name}.gdl")
        
        if not os.path.exists(gdl_path):
            print(f"⚠️ Skipping {game_name}: GDL file not found.")
            continue

        process_game_file(
            gdl_file_path=gdl_path,
            samples_file_path=samples_full_path,
            player=player,
            output_dir=output_dir,
            max_samples=args.max_samples,
            experiment_type=args.experiment,
            n_moves=args.n_moves
        )
        
        time.sleep(2)

if __name__ == "__main__":
    main()