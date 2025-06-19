import os
import zipfile
import sys
import shutil

def zip_folder_without_meta(folder_path, output_zip_path):
    with zipfile.ZipFile(output_zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(folder_path):
            # Ignore .idea folder
            if '.idea' in dirs:
                dirs.remove('.idea')
            for file in files:
                if not file.endswith('.meta'):
                    file_path = os.path.join(root, file)
                    arcname = os.path.relpath(file_path, start=folder_path)
                    zipf.write(file_path, arcname)
    print(f"Zip créé : {output_zip_path}")

def unzip_and_replace(zip_path, extract_to):
    if os.path.exists(extract_to):
        shutil.rmtree(extract_to)
    with zipfile.ZipFile(zip_path, 'r') as zipf:
        zipf.extractall(extract_to)
    print(f"Dézippé dans : {extract_to}")

if __name__ == "__main__":
    if len(sys.argv) < 2 or len(sys.argv) > 3:
        print("Usage: python script.py <dossier> [-a]")
        sys.exit(1)

    input_folder = sys.argv[1]
    do_unzip = len(sys.argv) == 3 and sys.argv[2] == "-a"

    if not os.path.isdir(input_folder):
        print(f"Erreur : le dossier '{input_folder}' n'existe pas.")
        sys.exit(1)

    output_zip = os.path.basename(os.path.normpath(input_folder)) + ".zip"
    zip_folder_without_meta(input_folder, output_zip)

    if do_unzip:
        extract_folder = os.path.splitext(output_zip)[0]
        unzip_and_replace(output_zip, extract_folder)
        # Supprimer le zip après extraction
        if os.path.exists(output_zip):
            os.remove(output_zip)
            print(f"Zip supprimé : {output_zip}")