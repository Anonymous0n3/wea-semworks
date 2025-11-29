document.addEventListener('DOMContentLoaded', () => {
    loadPublicWidgets(1);
});

let currentPage = 1;

async function loadPublicWidgets(page) {
    currentPage = page;
    const filter = {
        searchName: document.getElementById('filterName').value,
        author: document.getElementById('filterAuthor').value,
        widgetType: document.getElementById('filterType').value,
        sortBy: document.getElementById('sortOrder').value,
        page: page,
        pageSize: 20
    };

    try {
        const resp = await fetch('/api/PublicWidgets/list', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(filter)
        });
        const data = await resp.json();
        renderWidgets(data);
        renderPagination(page, data.length); // Zjednodušené, v praxi API vrací totalCount
    } catch (err) {
        console.error("Failed to load widgets", err);
    }
}

function renderWidgets(widgets) {
    const container = document.getElementById('publicWidgetsList');
    container.innerHTML = '';
    const currentUser = localStorage.getItem('userEmail'); // Předpokládá uložení emailu po loginu
    const token = localStorage.getItem('jwtToken');

    widgets.forEach(w => {
        const isAuthor = w.authorEmail === currentUser;

        // Kartička widgetu
        const col = document.createElement('div');
        col.className = 'col-md-6 col-lg-4 mb-4';

        let settingsSummary = `Typ: ${w.widgetType}`;
        if (w.widgetData.location) settingsSummary += `<br>Lokace: ${w.widgetData.location}`;

        col.innerHTML = `
            <div class="card h-100 shadow-sm">
                <div class="card-body">
                    <h5 class="card-title">${w.publicName}</h5>
                    <h6 class="card-subtitle mb-2 text-muted">Autor: ${w.authorName}</h6>
                    <p class="card-text small">${settingsSummary}</p>
                    <div class="d-flex justify-content-between align-items-center mt-3">
                        <div>
                            <span class="badge bg-secondary me-2">${w.likesCount} Likes</span>
                            <small class="text-muted">${new Date(w.createdAt).toLocaleDateString()}</small>
                        </div>
                    </div>
                </div>
                <div class="card-footer bg-white border-top-0 d-flex justify-content-between">
                    ${!isAuthor && token ? `<button class="btn btn-sm btn-outline-danger" onclick="likeWidget('${w.id}')">♥ Like</button>` : ''}
                    ${token ? `<button class="btn btn-sm btn-success" onclick='adoptWidget(${JSON.stringify(w.widgetData)})'>+ Přidat na můj Dashboard</button>` : '<small>Přihlas se pro přidání</small>'}
                </div>
            </div>
        `;
        container.appendChild(col);
    });
}

async function likeWidget(id) {
    const token = localStorage.getItem('jwtToken');
    if (!token) return alert("Musíte být přihlášen.");

    await fetch(`/api/PublicWidgets/${id}/like`, {
        method: 'POST',
        headers: { 'Authorization': 'Bearer ' + token }
    });
    loadPublicWidgets(currentPage); // Reload pro aktualizaci počtu
}

function adoptWidget(widgetData) {
    // 1. Načíst stávající widgety z localStorage
    let currentWidgets = JSON.parse(localStorage.getItem("openWidgets") || "[]");

    // 2. Přidat nový (zkopírování nastavení)
    // Ujistíme se, že je formát kompatibilní s UserWidgetState
    const newWidget = {
        name: widgetData.name, // Case sensitive match s UserWidgetState
        location: widgetData.location || ""
    };

    currentWidgets.push(newWidget);
    localStorage.setItem("openWidgets", JSON.stringify(currentWidgets));

    // 3. Pokud je online, uložíme i do DB (využijeme existující funkci v site.js logice nebo reloadneme)
    // Nejjednodušší: přesměrovat uživatele na jeho dashboard, kde se to uloží
    if (confirm("Widget byl přidán! Přejít na Můj Dashboard?")) {
        window.location.href = "/";
    }
}

function renderPagination(page, count) {
    const nav = document.getElementById('paginationControls');
    nav.innerHTML = '';

    // Prev
    if (page > 1) {
        nav.innerHTML += `<li class="page-item"><button class="page-link" onclick="loadPublicWidgets(${page - 1})">Předchozí</button></li>`;
    }

    // Next (zjednodušené - pokud jsme dostali plnou stránku, asi je další)
    if (count === 20) {
        nav.innerHTML += `<li class="page-item"><button class="page-link" onclick="loadPublicWidgets(${page + 1})">Další</button></li>`;
    }
}