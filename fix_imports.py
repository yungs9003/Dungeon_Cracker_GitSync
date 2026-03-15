import os
import re

# Root Scripts directory
SCRIPTS_ROOT = r'C:\Users\yoong\Dungeon 2025-11-29_23-04-34\Dungeon\Assets\Scripts'

# Regex to find class/interface/enum definitions inside namespace
# Capture group 1: namespace
# Capture group 4: class/interface/enum name
# Limitations: Finds one per file usually, or simplifies. We assume one main class per file usually matches file name, generally safe for Unity.
# Actually, better to just scan for 'namespace X' and then 'class Y', 'interface Z', 'enum W'
DEF_REGEX = re.compile(r'namespace\s+([\w\.]+)')
TYPE_REGEX = re.compile(r'\b(class|interface|enum|struct)\s+(\w+)')

def build_class_map(root_dir):
    """
    Returns a dict: { 'ClassName': 'Namespace' }
    """
    class_map = {}
    
    print("Building class map...")
    for root, _, files in os.walk(root_dir):
        for file in files:
            if not file.endswith(".cs"): continue
            
            file_path = os.path.join(root, file)
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()
            except:
                with open(file_path, 'r', encoding='cp949') as f:
                    content = f.read()

            # Find namespace
            ns_match = DEF_REGEX.search(content)
            if not ns_match: continue
            
            namespace = ns_match.group(1)
            
            # Find all types defined in this file
            for match in TYPE_REGEX.finditer(content):
                type_name = match.group(2)
                # Avoid mapping private classes if possible, but public is default assumption for this script
                # Also prevent duplicate mapping collisions (first wins or last wins? we'll use first for now)
                if type_name not in class_map:
                    class_map[type_name] = namespace
                    # print(f"  Mapped {type_name} -> {namespace}")
                    
    print(f"Mapped {len(class_map)} types.")
    return class_map

def fix_file(file_path, class_map):
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
            content = "".join(lines)
    except:
        with open(file_path, 'r', encoding='cp949') as f:
            lines = f.readlines()
            content = "".join(lines)
            
    # Find current namespace of this file
    ns_match = DEF_REGEX.search(content)
    current_ns = ns_match.group(1) if ns_match else ""
    
    # Identify existing usings
    existing_usings = set()
    last_using_idx = -1
    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("using ") and ";" in stripped:
            imported_ns = stripped.replace("using", "").replace(";", "").strip()
            existing_usings.add(imported_ns)
            last_using_idx = i
            
    # Determine where to insert new usings
    insert_idx = last_using_idx + 1 if last_using_idx >= 0 else 0
    
    new_usings = set()
    
    # Check for usage of known types
    for type_name, target_ns in class_map.items():
        # If the type is in the same namespace, skip
        if target_ns == current_ns: continue
        
        # If already imported, skip
        if target_ns in existing_usings: continue
        
        # Check if type is used in the file
        # Regex: word boundary + type_name + word boundary
        # We assume usage if the name appears. False positives possible (variable names same as class names)
        # but in C# usually classes are PascalCase and vars are camelCase, lowering risk.
        if re.search(r'\b' + re.escape(type_name) + r'\b', content):
            new_usings.add(target_ns)
            
    if not new_usings:
        return False
        
    # Add new usings
    added_lines = [f"using {ns};\n" for ns in sorted(new_usings)]
    
    # Insert
    lines.insert(insert_idx, "".join(added_lines))
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(lines)
        
    print(f"Fixed {os.path.basename(file_path)}: Added {', '.join(sorted(new_usings))}")
    return True

def main():
    class_map = build_class_map(SCRIPTS_ROOT)
    
    modified_count = 0
    for root, _, files in os.walk(SCRIPTS_ROOT):
        for file in files:
            if not file.endswith(".cs"): continue
            file_path = os.path.join(root, file)
            if fix_file(file_path, class_map):
                modified_count += 1
                
    print(f"\nDone! Fixed imports in {modified_count} files.")

if __name__ == "__main__":
    main()
