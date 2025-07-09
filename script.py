import os
import sys
import zipfile
import shutil

# --- CONFIGURATION ---
NOM_FICHIER_BUNDLE = "Code_Bundle.txt"
EXTENSION_A_INCLURE = ".cs"
DOSSIERS_A_EXCLURE_BUNDLE = ["bin", "obj", ".vs", ".idea", "Logs", "UserSettings", "Nodes", "UI", "User Interface"]
DOSSIERS_A_EXCLURE_ZIP = ['.idea', 'Logs', 'Library', 'Temp', 'obj', 'Build', 'UserSettings']
FICHIERS_A_EXCLURE_ZIP = ['.sln', '.user', '.csproj.DotSettings', '.meta']
# --- FIN DE LA CONFIGURATION ---

def afficher_menu(dossier_cible):
    """Affiche le menu interactif pour le dossier cible."""
    nom_dossier_affichage = os.path.basename(dossier_cible).upper()
    while True:
        print("\n" + "="*50)
        print(f"  DOSSIER CIBLE : {dossier_cible}")
        print("="*50)
        print("  1. Consolider le code en un seul fichier (.txt)")
        print("  2. Créer une archive ZIP simple du projet")
        print("  3. Quitter")
        print("="*50)

        choix = input("Votre choix [1-3] : ")
        if choix == '1':
            creer_bundle(dossier_cible)
        elif choix == '2':
            lancer_creation_zip(dossier_cible, auto_unzip=False)
        elif choix == '3':
            print("Au revoir !")
            break
        else:
            print("Choix invalide.")

def creer_bundle(dossier_cible):
    """Combine les fichiers de code en un seul fichier texte."""
    # Le fichier de sortie est créé dans le dossier d'où le script est exécuté.
    dossier_execution = os.getcwd()
    chemin_sortie = os.path.join(dossier_execution, NOM_FICHIER_BUNDLE)

    print(f"\n--- Consolidation du code de : {dossier_cible} ---")
    print(f"Fichier de sortie : {chemin_sortie}")

    fichiers_traites = 0
    try:
        with open(chemin_sortie, "w", encoding="utf-8") as outfile:
            for dossier_parent, sous_dossiers, _ in os.walk(dossier_cible):
                sous_dossiers[:] = [d for d in sous_dossiers if d not in DOSSIERS_A_EXCLURE_BUNDLE]
                for nom_fichier in os.listdir(dossier_parent):
                    if nom_fichier.endswith(EXTENSION_A_INCLURE):
                        chemin_complet = os.path.join(dossier_parent, nom_fichier)
                        chemin_relatif = os.path.relpath(chemin_complet, dossier_cible).replace(os.sep, '/')

                        outfile.write(f"// --- FILE: {chemin_relatif} ---\n")
                        with open(chemin_complet, "r", encoding="utf-8", errors='ignore') as infile:
                            outfile.write(infile.read())
                            outfile.write("\n\n")
                        fichiers_traites += 1
    except Exception as e:
        print(f"*** ERREUR lors de la consolidation : {e}")
        return
    print(f"\n🎉 Consolidation terminée ! {fichiers_traites} fichiers ajoutés.")

def lancer_creation_zip(dossier_cible, auto_unzip=False):
    """Gère la création du ZIP et l'option de décompression automatique."""
    dossier_execution = os.getcwd()
    nom_dossier = os.path.basename(os.path.normpath(dossier_cible))
    chemin_zip_sortie = os.path.join(dossier_execution, f"{nom_dossier}.zip")

    print(f"\n--- Création de l'archive ZIP pour : {dossier_cible} ---")
    print(f"Archive de sortie : {chemin_zip_sortie}")

    if zip_dossier(dossier_cible, chemin_zip_sortie):
        if auto_unzip:
            print("\n--- Mode automatique activé ---")
            dossier_extraction = os.path.join(dossier_execution, nom_dossier)
            unzip_and_replace(chemin_zip_sortie, dossier_extraction)

def zip_dossier(chemin_dossier, chemin_zip):
    """Crée l'archive ZIP en excluant les fichiers non désirés."""
    try:
        with zipfile.ZipFile(chemin_zip, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for root, dirs, files in os.walk(chemin_dossier):
                dirs[:] = [d for d in dirs if d not in DOSSIERS_A_EXCLURE_ZIP]
                for file in files:
                    if not any(file.endswith(ext) for ext in FICHIERS_A_EXCLURE_ZIP):
                        file_path = os.path.join(root, file)
                        arcname = os.path.relpath(file_path, start=chemin_dossier)
                        zipf.write(file_path, arcname)
        print(f"🎉 Archive ZIP créée avec succès.")
        return True
    except Exception as e:
        print(f"*** ERREUR lors de la création du ZIP : {e}")
        return False

def unzip_and_replace(zip_path, extract_to):
    """Dézippe une archive, supprime le dossier de destination s'il existe, puis supprime l'archive."""
    if os.path.exists(extract_to):
        print(f"Suppression du dossier existant : {extract_to}")
        shutil.rmtree(extract_to)

    print(f"Décompression de l'archive dans : {extract_to}")
    with zipfile.ZipFile(zip_path, 'r') as zipf:
        zipf.extractall(extract_to)

    if os.path.exists(zip_path):
        print(f"Suppression de l'archive : {zip_path}")
        os.remove(zip_path)

# --- POINT D'ENTRÉE DU SCRIPT ---
if __name__ == "__main__":
    args = sys.argv[1:]

    if not args:
        # Mode interactif si aucun argument n'est fourni
        dossier_actuel = os.getcwd()
        print("Aucun chemin fourni, utilisation du dossier courant.")
        afficher_menu(dossier_actuel)
        sys.exit(0)

    # Vérification du chemin et du flag -a
    dossier_cible_arg = ""
    auto_unzip_flag = "-a" in args

    # Trouver le chemin parmi les arguments
    for arg in args:
        if arg != "-a" and os.path.isdir(arg):
            dossier_cible_arg = os.path.abspath(arg)
            break

    if not dossier_cible_arg:
        print("Erreur : Le chemin fourni n'est pas un dossier valide ou est manquant.")
        print(r"Usage : python script.py 'C:\chemin\valide' [-a]")
        sys.exit(1)

    # Si le flag -a est présent, on exécute directement la fonction de zip
    if auto_unzip_flag:
        lancer_creation_zip(dossier_cible_arg, auto_unzip=True)
    # Sinon, on lance le menu interactif pour le dossier spécifié
    else:
        afficher_menu(dossier_cible_arg)