import sys
from pathlib import Path

# Add the src directory to the Python path
src_path = Path(__file__).parent
sys.path.append(str(src_path))