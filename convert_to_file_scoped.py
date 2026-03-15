import os
import re

SCRIPTS_ROOT = r'C:\Users\yoong\Dungeon 2025-11-29_23-04-34\Dungeon\Assets\Scripts'

def convert_file(file_path):
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except:
        try:
            with open(file_path, 'r', encoding='cp949') as f:
                lines = f.readlines()
        except:
            return False

    content = "".join(lines)
    
    # Check if it has block-scoped namespace
    # namespace X
    # {
    #    ...
    # }
    
    # Regex to find "namespace Name \n {" or "namespace Name {"
    # We want to capture the name
    match = re.search(r'namespace\s+([\w\.]+)\s*\n?\s*\{', content)
    if not match:
        print(f"Skipping {os.path.basename(file_path)}: No block namespace found.")
        return False
        
    namespace_name = match.group(1)
    
    # We will reconstruct the file
    new_lines = []
    
    # 1. Keep imports
    # 2. Add 'namespace X;'
    # 3. Dedent body
    # 4. Remove last brace
    
    # Simplified approach: iterating lines
    inside_namespace = False
    
    # Find start and end indices of the namespace block
    # Actually, since we know we just formatted it, let's assume standard structure:
    # imports...
    # namespace X
    # {
    #    body
    # }
    
    start_brace_index = -1
    for i, line in enumerate(lines):
        if line.strip() == "namespace " + namespace_name:
            # Check next line for brace
            if i+1 < len(lines) and lines[i+1].strip() == "{":
                start_brace_index = i+1
                ns_decl_index = i
                break
        elif line.strip() == "namespace " + namespace_name + " {": # inline brace case (rare in my previous script but possible)
             pass # Logic needs to be robust, but my script did newline brace
        elif line.strip() == "namespace " + namespace_name + "{":
             pass

    # Better approach: Iterate and transform state
    
    processed_lines = []
    found_ns = False
    brace_open_found = False
    
    for i, line in enumerate(lines):
        stripped = line.strip()
        
        # Detect namespace declaration
        if not found_ns and stripped.startswith("namespace ") and namespace_name in stripped:
            processed_lines.append(f"namespace {namespace_name};\n")
            found_ns = True
            continue
            
        # Detect opening brace of namespace
        if found_ns and not brace_open_found and stripped == "{":
            brace_open_found = True
            continue
            
        # If we are inside namespace (after brace open), dedent
        if brace_open_found:
            # Remove 4 spaces of indentation if present
            if line.startswith("    "):
                processed_lines.append(line[4:])
            else:
                processed_lines.append(line)
        else:
            # Before namespace (imports)
            processed_lines.append(line)
            
    # Remove the last closing brace '}'
    # Reverse look for the first '}' on a line by itself
    for i in range(len(processed_lines) - 1, -1, -1):
        if processed_lines[i].strip() == "}":
            processed_lines.pop(i)
            break
            
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(processed_lines)
        
    print(f"Converted {os.path.basename(file_path)}")
    return True

def main():
    modified_count = 0
    for root, _, files in os.walk(SCRIPTS_ROOT):
        for file in files:
            if not file.endswith(".cs"): continue
            file_path = os.path.join(root, file)
            if convert_file(file_path):
                modified_count += 1
                
    print(f"\nDone! Converted {modified_count} files to file-scoped namespaces.")

if __name__ == "__main__":
    main()
