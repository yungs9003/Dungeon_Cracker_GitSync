import os
import re

# Namespace mapping based on folder names
NAMESPACE_MAP = {
    'Player': 'Dungeon.Game.Player',
    'GetToWork': 'Dungeon.Game.GetToWork', # Extra check if needed, but per map:
    'UI': 'Dungeon.Game.UI',
    'Traps': 'Dungeon.Game.Traps',
    'Map': 'Dungeon.Game.Map',
    'Core': 'Dungeon.Game.Core',
    'Manager': 'Dungeon.Game.Manager',
    'System': 'Dungeon.Game.System',
}

# Root Scripts directory
SCRIPTS_ROOT = r'C:\Users\yoong\Dungeon 2025-11-29_23-04-34\Dungeon\Assets\Scripts'

def apply_namespace(file_path, namespace_name):
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except UnicodeDecodeError:
        # Fallback to euc-kr or cp949 if utf-8 fails (common for legacy Korean windows files)
        try:
            with open(file_path, 'r', encoding='cp949') as f:
                lines = f.readlines()
        except Exception as e:
            print(f"Skipping {file_path}: Cannot read file. Error: {e}")
            return False

    # Check if namespace is already applied (simple check)
    content = "".join(lines)
    if f"namespace {namespace_name}" in content:
        print(f"Skipping {file_path}: Namespace already present.")
        return False

    using_lines = []
    other_lines = []
    
    # Simple parser to separate using directives
    # Note: This does not handle nested usings inside comments perfectly but is sufficient for Unity scripts
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("using ") and ";" in stripped:
             using_lines.append(line)
        else:
             other_lines.append(line)

    # Reconstruct file content
    new_content = []
    
    # 1. Add existing using statements
    new_content.extend(using_lines)
    if using_lines:
        new_content.append("\n") # Add spacer
        
    # 2. Add namespace declaration
    new_content.append(f"namespace {namespace_name}\n")
    new_content.append("{\n")
    
    # 3. Add indented code
    # Find the first non-empty line of the body to determine start (skipping empty lines at top of other_lines)
    start_idx = 0
    while start_idx < len(other_lines) and not other_lines[start_idx].strip():
        start_idx += 1
        
    remaining_lines = other_lines[start_idx:]
    
    for line in remaining_lines:
        # Indent line if it's not empty, otherwise just newline
        if line.strip():
            new_content.append("    " + line)
        else:
            new_content.append(line)
            
    # 4. Close namespace
    # Ensure there is a newline before closing brace if the file didn't end with one
    if new_content[-1].strip():
        new_content.append("\n")
    new_content.append("}\n")
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(new_content)
        
    return True

def main():
    modified_count = 0
    for subdir, namespace in NAMESPACE_MAP.items():
        dir_path = os.path.join(SCRIPTS_ROOT, subdir)
        
        if not os.path.exists(dir_path):
            print(f"Directory not found: {dir_path}")
            continue
            
        print(f"Processing {subdir} -> {namespace}...")
        
        for root, _, files in os.walk(dir_path):
            for file in files:
                if file.endswith(".cs"):
                    file_path = os.path.join(root, file)
                    print(f"  Applying to {file}...")
                    if apply_namespace(file_path, namespace):
                        modified_count += 1
                        
    print(f"\nDone! Modified {modified_count} files.")

if __name__ == "__main__":
    main()
