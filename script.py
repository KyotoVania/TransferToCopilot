import os
import zipfile
import sys

def zip_folder_without_meta(folder_path, output_zip_path):
    with zipfile.ZipFile(output_zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(folder_path):
            for file in files:
                if not file.endswith('.meta'):
                    file_path = os.path.join(root, file)
                    arcname = os.path.relpath(file_path, start=folder_path)
                    zipf.write(file_path, arcname)
    print(f"Zip créé : {output_zip_path}")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python script.py <dossier>")
        sys.exit(1)

    input_folder = sys.argv[1]
    if not os.path.isdir(input_folder):
        print(f"Erreur : le dossier '{input_folder}' n'existe pas.")
        sys.exit(1)

    output_zip = os.path.basename(os.path.normpath(input_folder)) + ".zip"
    zip_folder_without_meta(input_folder, output_zip)
