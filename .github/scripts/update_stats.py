import os
import requests

# Nastavení
GITHUB_TOKEN = os.getenv('GITHUB_TOKEN')
REPO = os.getenv('GITHUB_REPOSITORY') # Format: "user/repo"
HEADERS = {'Authorization': f'token {GITHUB_TOKEN}'}

def get_contributors():
    url = f"https://api.github.com/repos/{REPO}/contributors"
    response = requests.get(url, headers=HEADERS)
    return response.json()

def generate_markdown(contributors):
    md = "| Uživatel | Počet commitů |\n| --- | ---: |\n"
    for user in contributors:
        login = user['login']
        contributions = user['contributions']
        avatar = user['avatar_url']
        # Vytvoří řádek: Avatar + Jméno | Počet
        md += f"| <img src='{avatar}' width='20'/> [{login}](https://github.com/{login}) | {contributions} |\n"
    return md

def update_readme(new_content):
    with open('README.md', 'r', encoding='utf-8') as file:
        content = file.read()

    start_marker = ""
    end_marker = ""
    
    start_index = content.find(start_marker) + len(start_marker)
    end_index = content.find(end_marker)

    if start_index == -1 or end_index == -1:
        print("Značky v README nenalezeny!")
        return

    updated_content = content[:start_index] + "\n" + new_content + "\n" + content[end_index:]

    with open('README.md', 'w', encoding='utf-8') as file:
        file.write(updated_content)

if __name__ == "__main__":
    data = get_contributors()
    table = generate_markdown(data)
    update_readme(table)
