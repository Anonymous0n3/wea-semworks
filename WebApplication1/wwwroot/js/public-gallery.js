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

function renderCard(w, currentUserEmail, token) {
    const isAuthor = w.authorEmail === currentUserEmail;
    // Escapování dat pro vložení do onclick
    const widgetDataStr = JSON.stringify(w.widgetData).replace(/"/g, '&quot;');
    // Escapování názvu pro bezpečné vložení do JS
    const safeName = w.publicName.replace(/'/g, "\\'");
    const widgetType = w.widgetType;

    let likesCount = w.likesCount || 0;

    // --- 1. LIKE TLAČÍTKO ---
    let likeSection = '';
    if (token) {
        const likedBy = w.likedBy || [];
        const isLiked = currentUserEmail && likedBy.some(e => e.toLowerCase() === currentUserEmail.toLowerCase());
        const btnClass = isLiked ? 'btn-danger' : 'btn-outline-danger';
        const icon = isLiked ? '❤️' : '🤍';

        if (!isAuthor) {
            likeSection = `
            <button type="button" class="btn btn-sm ${btnClass} position-relative" 
                    style="z-index: 5;"
                    onclick="event.stopPropagation(); window.toggleLike('${w.id}')">
                ${icon} ${likesCount}
            </button>`;
        } else {
            likeSection = `<span class="badge bg-light text-dark border" title="Vlastní widget">❤️ ${likesCount}</span>`;
        }
    } else {
        likeSection = `<span class="text-muted fw-bold">❤️ ${likesCount}</span>`;
    }

    // --- 2. TLAČÍTKO POUŽÍT (NÁHLED) ---
    let actionButtons = `
        <button class="btn btn-primary btn-sm w-100 mt-3" onclick="window.previewWidget('${widgetType}', ${widgetDataStr})">
            👁️ Vyzkoušet (Náhled)
        </button>
    `;

    // --- 3. NOVÉ TLAČÍTKO ZVLASTNIT (JEN PŘIHLÁŠENÍ) ---
    if (token) {
        actionButtons += `
        <button class="btn btn-success btn-sm w-100 mt-2" onclick="window.adoptWidget('${widgetType}', ${widgetDataStr}, '${safeName}')">
            💾 Zvlastnit (Uložit)
        </button>
        `;
    }

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm">
            <div class="card-body d-flex flex-column">
                <h5 class="card-title text-truncate" title="${w.publicName}">${w.publicName}</h5>
                <div class="mb-2">
                    <span class="badge bg-info text-dark">${w.widgetType}</span>
                </div>
                <p class="card-text small text-muted mb-1">Autor: ${w.authorName}</p>
                <p class="card-text small text-muted mb-auto">Lokalita: ${w.widgetData.location || "N/A"}</p>
                
                ${actionButtons}
            </div>
            <div class="card-footer bg-white border-top-0 d-flex justify-content-between align-items-center py-2">
                ${likeSection}
                <small class="text-muted">${new Date(w.createdAt).toLocaleDateString()}</small>
            </div>
        </div>
    </div>
    `;
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

function adoptWidget(widgetType, widgetData, widgetName) {
    // 1. Definice klíče, pod kterým Dashboard očekává data
    const STORAGE_KEY = 'dashboard_widgets';

    // 2. Načtení současných widgetů
    let currentWidgets = [];
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            currentWidgets = JSON.parse(stored);
            if (!Array.isArray(currentWidgets)) currentWidgets = [];
        }
    } catch (e) {
        console.error("Chyba při čtení localStorage", e);
        currentWidgets = [];
    }

    // 3. Vytvoření nového objektu widgetu
    // Struktura musí odpovídat tomu, co očekává tvůj Dashboard script!
    const newWidget = {
        id: 'imported_' + Date.now(), // Unikátní ID
        type: widgetType,
        data: widgetData,
        // Volitelně můžeš uložit i původní název, pokud ho dashboard zobrazuje
        title: widgetName
    };

    // 4. Přidání do pole a uložení
    currentWidgets.push(newWidget);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(currentWidgets));

    // 5. Zpětná vazba uživateli
    alert(`Widget "${widgetName}" byl úspěšně uložen! Najdete ho na svém Dashboardu.`);
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