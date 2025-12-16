import os
import requests
import time

# Nastavení
GITHUB_TOKEN = os.getenv('GITHUB_TOKEN')
REPO = os.getenv('GITHUB_REPOSITORY')
HEADERS = {'Authorization': f'token {GITHUB_TOKEN}'}

def get_contributors():
    """Stáhne seznam přispěvatelů a jejich celkové commity."""
    print("Stahuji přispěvatele...")
    url = f"https://api.github.com/repos/{REPO}/contributors"
    response = requests.get(url, headers=HEADERS)
    return response.json()

def get_branch_stats():
    """Zjistí, kdo je autorem posledního commitu na každé větvi."""
    print("Stahuji větve...")
    branch_counts = {}
    
    # 1. Získat seznam všech větví
    # Poznámka: Pokud je větví více než 30, GitHub používá stránkování. 
    # Pro jednoduchost zde stahujeme první stránku (max 100 větví).
    url = f"https://api.github.com/repos/{REPO}/branches?per_page=100"
    response = requests.get(url, headers=HEADERS)
    branches = response.json()

    # 2. Projít každou větev a zjistit autora posledního commitu
    print(f"Zpracovávám {len(branches)} větví...")
    for branch in branches:
        commit_url = branch['commit']['url']
        # Musíme se dotázat na detail commitu, abychom zjistili autora
        commit_resp = requests.get(commit_url, headers=HEADERS)
        
        if commit_resp.status_code == 200:
            commit_data = commit_resp.json()
            # Zkusíme získat login autora (pokud má GitHub účet)
            author = commit_data.get('author')
            if author:
                login = author['login']
                branch_counts[login] = branch_counts.get(login, 0) + 1
        
        # Malá pauza, abychom nezahltili API, pokud je větví hodně
        # time.sleep(0.1) 
    
    return branch_counts

def generate_markdown(contributors, branch_stats):
    """Vytvoří Markdown tabulku."""
    md = "| Uživatel | Počet commitů | Aktivní větve (last commit) |\n"
    md += "| --- | ---: | ---: |\n"
    
    for user in contributors:
        login = user['login']
        contributions = user['contributions']
        avatar = user['avatar_url']
        
        # Získáme počet větví pro tohoto uživatele (pokud žádné nemá, tak 0)
        branch_count = branch_stats.get(login, 0)
        
        # Pokud má uživatel 0 větví, zobrazíme pomlčku pro lepší čitelnost
        branch_display = branch_count if branch_count > 0 else "-"

        md += f"| <img src='{avatar}' width='20'/> [{login}](https://github.com/{login}) | {contributions} | {branch_display} |\n"
    return md

def update_readme(new_content):
    try:
        with open('README.md', 'r', encoding='utf-8') as file:
            content = file.read()
    except FileNotFoundError:
        print("Chyba: README.md nebyl nalezen.")
        return

    start_marker = ""
    end_marker = ""
    
    start_index = content.find(start_marker)
    end_index = content.find(end_marker)

    if start_index == -1 or end_index == -1:
        print("Značky v README nenalezeny! Ujistěte se, že tam jsou.")
        return

    # Sestavení nového obsahu: Text před značkou + Značka + Nová tabulka + Konec značky + Zbytek
    updated_content = (
        content[:start_index + len(start_marker)] + 
        "\n" + new_content + "\n" + 
        content[end_index:]
    )

    with open('README.md', 'w', encoding='utf-8') as file:
        file.write(updated_content)
    print("README.md byl úspěšně aktualizován.")

if __name__ == "__main__":
    contributors_data = get_contributors()
    branch_data = get_branch_stats()
    
    table = generate_markdown(contributors_data, branch_data)
    update_readme(table)
