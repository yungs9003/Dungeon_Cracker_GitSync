import os
import re

SCRIPTS_ROOT = r'C:\Users\yoong\Dungeon 2025-11-29_23-04-34\Dungeon\Assets\Scripts'

# Attribute to add
# Using UnityEngine.Scripting API
ATTRIBUTE = '[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]'

def add_moved_from(file_path):
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
    
    # Force update to fix incorrect attribute
    # if "MovedFrom" in content:
    #     return False
        
    # Pattern to find class/struct/enum/interface definition
    # We want to place the attribute right before it.
    # Supported: public class X, public struct X, public enum X, public interface X
    # Also internal...
    # We look for the main type definition in the file.
    # Since we are using file-scoped namespaces now, it's safer.
    
    # Regex for type definition
    # Group 1: The whole match up to the name
    # We will replace it with Attribute + \n + Match
    
    # Be careful not to match nested classes if possible, or just add to all?
    # Usually we only need it for the MonoBehaviours that are serialized in scenes/prefabs.
    # But adding to all top-level types is safer for references.
    
    type_regex = re.compile(r'^(\s*(?:public|internal|sealed|abstract|partial)*\s+(?:class|struct|enum|interface)\s+\w+)', re.MULTILINE)
    
    # We only want to decorate the top-level types (rarely nested types are serialized as separate files unless ScriptableObjects etc)
    # But regex multiline search finds all.
    # Let's simple-parse: find the first occurrence of "public class" or "public struct" etc.
    
    new_lines = []
    modified = False
    
    for line in lines:
        stripped = line.strip()
        # Heuristic: straightforward definitions
        if (stripped.startswith("public class") or 
            stripped.startswith("public struct") or 
            stripped.startswith("public enum") or 
            stripped.startswith("public interface") or
            stripped.startswith("public abstract class") or
            stripped.startswith("public sealed class") or
            stripped.startswith("[RequireComponent") or # attributes often precede
            stripped.startswith("[System.Serializable]") or
            stripped.startswith("[CreateAssetMenu]") or
            stripped.startswith("[Header") or
            stripped.startswith("[SerializeField")):
             
             # Wait, if there are existing attributes, we should put MovedFrom above them?
             # Or just above the class def is fine. Unity supports stacked attributes.
             # However, if we put it above [RequireComponent], it's fine.
             
             # Actually, simpler: Find the class definition line.
             pass

    # Better approach using content replacement with regex for the first match of class/struct/enum definition
    # This prevents adding to inner classes if we only target the first one, 
    # BUT multiple top-level types in one file is rare in Unity.
    
    # First, try to replace existing incorrect attribute
    INCORRECT_ATTR = '[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", null)]'
    if INCORRECT_ATTR in content:
        content = content.replace(INCORRECT_ATTR, ATTRIBUTE)
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Fixed {os.path.basename(file_path)}")
        return True

    match = type_regex.search(content)
    if match:
        original = match.group(1)
        # Check if it's a MonoBehaviour or ScriptableObject ideally, but we can't easily check inheritance without parsing.
        # Adding MovedFrom to POCOs (Plain Old C# Objects) won't hurt, just ignored if not serialized.
        
        replacement = f"{ATTRIBUTE}\n{original}"
        content = content.replace(original, replacement, 1) # Replace only first occurrence? 
        # Actually some files might have multiple types. But usually file name matches class name.
        # Let's stick to replacing the first one for safety, usually the main one.
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Updated {os.path.basename(file_path)}")
        return True
        
    return False

def main():
    modified_count = 0
    for root, _, files in os.walk(SCRIPTS_ROOT):
        for file in files:
            if not file.endswith(".cs"): continue
            file_path = os.path.join(root, file)
            if add_moved_from(file_path):
                modified_count += 1
                
    print(f"\nDone! Added MovedFrom to {modified_count} files.")

if __name__ == "__main__":
    main()
