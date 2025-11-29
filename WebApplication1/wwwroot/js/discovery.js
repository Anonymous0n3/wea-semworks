let currentPage = 1;

document.addEventListener('DOMContentLoaded', async () => {
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    const favSection = document.getElementById('favoritesSection');
    if (token) {
        favSection.classList.remove('d-none');
        await loadFavorites(token, userEmail);
    } else {
        favSection.classList.add('d-none');
    }

    await loadPublicList(1);
});

async function changePage(delta) {
    if (currentPage + delta < 1) return;
    currentPage += delta;
    document.getElementById('pageIndicator').innerText = `Strana ${currentPage}`;
    await loadPublicList(currentPage);
}

async function loadFavorites(token, userEmail) {
    const container = document.getElementById('favoritesContainer');
    try {
        const resp = await fetch('/api/PublicWidgets/liked', {
            headers: { 'Authorization': 'Bearer ' + token }
        });
        if (resp.ok) {
            const data = await resp.json();
            if (data.length === 0) container.innerHTML = '<div class="col-12 text-muted fst-italic">Zatím nic.</div>';
            else container.innerHTML = data.map(w => renderCard(w, userEmail, token, true)).join('');
        }
    } catch (e) { console.error(e); }
}

async function loadPublicList(page) {
    const container = document.getElementById('publicContainer');
    const search = document.getElementById('discoSearch').value;
    const author = document.getElementById('discoAuthor').value;
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    const filter = {
        searchName: search,
        author: author,
        page: page,
        pageSize: 12,
        sortBy: "date"
    };

    try {
        const resp = await fetch('/api/PublicWidgets/list', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(filter)
        });
        if (resp.ok) {
            const data = await resp.json();
            if (data.length === 0 && page > 1) { currentPage--; return; } // Konec listu
            container.innerHTML = data.map(w => renderCard(w, userEmail, token, false)).join('');
        }
    } catch (e) { console.error(e); }
}

function renderCard(w, currentUserEmail, token, isFavSection) {
    const isAuthor = w.authorEmail === currentUserEmail;
    const widgetDataStr = JSON.stringify(w.widgetData).replace(/"/g, '&quot;');

    let likeBtn = '';
    if (token && !isAuthor) {
        const icon = isFavSection ? '❤️' : '🤍';
        likeBtn = `<button class="btn btn-sm btn-light border position-absolute top-0 end-0 m-2" onclick="toggleLike('${w.id}')" title="Like">${icon}</button>`;
    }

    let addBtn = '';
    if (token) {
        addBtn = `<button class="btn btn-primary btn-sm w-100 mt-2" onclick="adoptWidget(${widgetDataStr})">Použít</button>`;
    } else {
        addBtn = `<small class="d-block mt-2 text-muted">Přihlaste se</small>`;
    }

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm position-relative">
            ${likeBtn}
            <div class="card-body">
                <h5 class="card-title text-truncate pe-4">${w.publicName}</h5>
                <span class="badge bg-light text-dark border mb-2">${w.widgetType}</span>
                <p class="card-text small text-muted mb-0">Autor: ${w.authorName}</p>
                <p class="card-text small text-muted">Lokalita: ${w.widgetData.location || "N/A"}</p>
                ${addBtn}
            </div>
            <div class="card-footer bg-white border-top-0 text-muted small d-flex justify-content-between">
                <span>❤️ ${w.likesCount}</span>
                <span>${new Date(w.createdAt).toLocaleDateString()}</span>
            </div>
        </div>
    </div>
    `;
}

async function toggleLike(id) {
    const token = localStorage.getItem('jwtToken');
    if (!token) return;
    await fetch(`/api/PublicWidgets/${id}/like`, {
        method: 'POST',
        headers: { 'Authorization': 'Bearer ' + token }
    });
    const userEmail = localStorage.getItem('userEmail');
    loadFavorites(token, userEmail);
    // Reload public list only if necessary to update counts, optional
    loadPublicList(currentPage);
}

function adoptWidget(widgetData) {
    let current = JSON.parse(localStorage.getItem("openWidgets") || "[]");
    current.push({
        name: widgetData.Name,
        location: widgetData.Location || ""
    });
    localStorage.setItem("openWidgets", JSON.stringify(current));

    // PŘESMĚROVÁNÍ NA DASHBOARD (Index)
    window.location.href = "/";
}