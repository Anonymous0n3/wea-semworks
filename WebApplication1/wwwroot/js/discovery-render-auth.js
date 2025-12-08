// discovery-render-auth.js

export function renderCard(w, userEmail) {
    const isAuthor = w.authorEmail === userEmail;
    // Escapování dat
    const safeName = w.publicName.replace(/'/g, "\\'");
    const widgetDataStr = JSON.stringify(w.widgetData).replace(/"/g, '&quot;');
    let likesCount = w.likesCount || 0;

    // Logika pro Like tlačítko
    let likeHtml = '';
    if (isAuthor) {
        likeHtml = `<span class="badge bg-light text-dark border" title="Vlastní widget">❤️ ${likesCount}</span>`;
    } else {
        const likedBy = w.likedBy || [];
        const isLiked = userEmail && likedBy.some(e => e.toLowerCase() === userEmail.toLowerCase());
        const btnClass = isLiked ? 'btn-danger' : 'btn-outline-danger';
        const icon = isLiked ? '❤️' : '🤍';

        likeHtml = `
            <button type="button" class="btn btn-sm ${btnClass} position-relative" 
                    style="z-index: 5;"
                    onclick="event.stopPropagation(); window.toggleLike('${w.id}')">
                ${icon} ${likesCount}
            </button>`;
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
                
                <button class="btn btn-primary btn-sm w-100 mt-3" onclick="window.previewWidget('${w.widgetType}', ${widgetDataStr})">
                    👁️ Vyzkoušet (Náhled)
                </button>

                <button class="btn btn-success btn-sm w-100 mt-2" onclick="window.adoptWidget('${w.widgetType}', ${widgetDataStr}, '${safeName}')">
                    💾 Zvlastnit (Uložit)
                </button>
            </div>
            <div class="card-footer bg-white border-top-0 d-flex justify-content-between align-items-center py-2">
                ${likeHtml}
                <small class="text-muted">${new Date(w.createdAt).toLocaleDateString()}</small>
            </div>
        </div>
    </div>
    `;
}

export function renderEmptyState() {
    return '<div class="col-12 text-muted fst-italic">Zatím nemáte žádné oblíbené widgety.</div>';
}