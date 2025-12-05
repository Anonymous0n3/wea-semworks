let currentPage = 1;

// Při načtení stránky
document.addEventListener('DOMContentLoaded', async () => {
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    // Oblíbené sekce
    const favSection = document.getElementById('favoritesSection');
    if (token && favSection) {
        favSection.classList.remove('d-none');
        await loadFavorites(token, userEmail);
    } else if (favSection) {
        favSection.classList.add('d-none');
    }

    // Načti veřejné widgety
    await loadPublicList(1);

    // Automatické vyhledávání při změně filtrů
    ['discoSearch', 'discoAuthor', 'discoType', 'discoSort'].forEach(id => {
        const el = document.getElementById(id);
        if (el) {
            el.addEventListener('input', () => loadPublicList(1));
            el.addEventListener('change', () => loadPublicList(1));
        }
    });
});

async function changePage(delta) {
    const newPage = currentPage + delta;
    if (newPage < 1) return;
    currentPage = newPage;
    document.getElementById('pageIndicator').innerText = `Strana ${currentPage}`;
    await loadPublicList(currentPage);
}

// --- NAČÍTÁNÍ VEŘEJNÝCH WIDGETŮ S FILTRY ---
async function loadPublicList(page) {
    const container = document.getElementById('publicContainer');
    if (!container) return;

    const search = document.getElementById('discoSearch')?.value.trim() || "";
    const author = document.getElementById('discoAuthor')?.value.trim() || "";
    const type = document.getElementById('discoType')?.value || "";
    const sort = document.getElementById('discoSort')?.value || "date";

    const filter = {
        searchName: search,
        author: author,
        widgetType: type,        // ← NOVÉ
        sortBy: sort,            // ← "date" nebo "likes"
        page: page,
        pageSize: 20            // ← zvýšeno z 12 na 20 podle zadání
    };

    try {
        const resp = await fetch('/api/PublicWidgets/list', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(filter)
        });

        if (resp.ok) {
            const data = await resp.json();
            container.innerHTML = data.map(w => renderCard(w, localStorage.getItem('userEmail'), localStorage.getItem('jwtToken'))).join('');

            // Stránkování – pokud vrátilo méně než 20, už není další strana
            const hasMore = data.length === 20;
            document.querySelector('button[onclick="changePage(1)"]')?.setAttribute('disabled', !hasMore);
            document.querySelector('button[onclick="changePage(-1)"]')?.setAttribute('disabled', page <= 1);
        } else {
            container.innerHTML = '<div class="col-12 text-danger">Chyba při načítání widgetů</div>';
        }
    } catch (e) {
        console.error(e);
        container.innerHTML = '<div class="col-12 text-danger">Nelze se připojit k serveru</div>';
    }
}

// --- ZBYTEK TVOJÍ FUNKCE renderCard BEZE ZMĚNY (jen drobně vylepšeno) ---
function renderCard(w, currentUserEmail, token) {
    const isAuthor = w.authorEmail?.toLowerCase() === currentUserEmail?.toLowerCase();
    const widgetDataStr = JSON.stringify(w.widgetData || {}).replace(/"/g, '&quot;');
    const safeName = (w.publicName || "Můj widget").replace(/'/g, "\\'");
    const widgetType = w.widgetType;

    let likeSection = '';
    if (token) {
        const likedBy = w.likedBy || [];
        const isLiked = currentUserEmail && likedBy.some(e => e.toLowerCase() === currentUserEmail.toLowerCase());
        const btnClass = isLiked ? 'btn-danger' : 'btn-outline-danger';
        const icon = isLiked ? 'Liked' : 'Like';

        if (!isAuthor) {
            likeSection = `
            <button type="button" class="btn btn-sm ${btnClass} position-relative"
                    onclick="event.stopPropagation(); window.toggleLike('${w.id}')">
                ${icon} ${w.likesCount || 0}
            </button>`;
        } else {
            likeSection = `<span class="badge bg-light text-dark border">Owned ${w.likesCount || 0}</span>`;
        }
    } else {
        likeSection = `<span class="text-muted fw-bold">Likes ${w.likesCount || 0}</span>`;
    }

    const actionButtons = token ? `
        <button class="btn btn-success btn-sm w-100 mt-2" onclick="event.stopPropagation(); window.adoptWidget('${widgetType}', ${widgetDataStr}, '${safeName}')">
            Save to My Dashboard
        </button>` : '';

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm">
            <div class="card-body d-flex flex-column">
                <h5 class="card-title text-truncate" title="${w.publicName}">${w.publicName}</h5>
                <div class="mb-2">
                    <span class="badge bg-info text-dark">${w.widgetType}</span>
                </div>
                <p class="card-text small text-muted mb-1">Autor: ${w.authorName || w.authorEmail}</p>
                <p class="card-text small text-muted mb-auto">${w.widgetData?.location || "—"}</p>

                <button class="btn btn-primary btn-sm w-100" onclick="event.stopPropagation(); window.previewWidget('${widgetType}', ${widgetDataStr})">
                    Preview
                </button>
                ${actionButtons}
            </div>
            <div class="card-footer bg-white border-top-0 d-flex justify-content-between align-items-center py-2">
                ${likeSection}
                <small class="text-muted">${new Date(w.createdAt).toLocaleDateString('cs-CZ')}</small>
            </div>
        </div>
    </div>`;
}

// --- LIKE (beze změny) ---
async function toggleLike(id) {
    const token = localStorage.getItem('jwtToken');
    if (!token) return alert("Musíš být přihlášený!");
    try {
        const resp = await fetch(`/api/PublicWidgets/${id}/like`, {
            method: 'POST',
            headers: { 'Authorization': 'Bearer ' + token }
        });
        if (resp.ok) {
            await loadPublicList(currentPage);
            const userEmail = localStorage.getItem('userEmail');
            loadFavorites(token, userEmail);
        }
    } catch (e) { console.error(e); }
}

// --- ADOPT (zvlastnění) – už máš hotové, jen malá pojistka ---
function adoptWidget(widgetType, widgetData, publicName) {
    const STORAGE_KEY = 'openWidgets';
    let currentWidgets = [];
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) currentWidgets = JSON.parse(stored);
        if (!Array.isArray(currentWidgets)) currentWidgets = [];
    } catch (e) { currentWidgets = []; }

    currentWidgets.push({
        name: widgetType,
        location: widgetData.location || "",
        publicName: publicName // volitelné – pro lepší přehled
    });

    localStorage.setItem(STORAGE_KEY, JSON.stringify(currentWidgets));
    alert(`Widget "${publicName}" byl přidán do tvého dashboardu!`);
}

// Zbytek funkcí (previewWidget, closePreview, initWidgetScripts) zůstává **přesně tak, jak je máš** – fungují perfektně.

// Export do globálního scope
window.toggleLike = toggleLike;
window.previewWidget = previewWidget;
window.adoptWidget = adoptWidget;
window.closePreview = closePreview;
window.changePage = changePage; // aby fungovalo tlačítko stránkování