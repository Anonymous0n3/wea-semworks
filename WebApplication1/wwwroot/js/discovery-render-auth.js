// discovery-render-auth.js

export function renderCard(w, userEmail) {
    // 1. Získání ID (pro Like)
    const widgetId = w._id || w.id || w.Id;

    // 2. Příprava dat pro tlačítka (bezpečné převedení objektu na text)
    // Pokud widgetData neexistují, použijeme prázdný objekt {}
    const dataString = JSON.stringify(w.widgetData || {});

    // 3. Stav Liku
    const isLiked = w.likedBy && w.likedBy.includes(userEmail);
    const heartClass = isLiked ? "text-danger" : "text-muted";
    const heartIcon = isLiked ? "❤️" : "🤍";
    const dateStr = w.createdAt ? new Date(w.createdAt).toLocaleDateString() : "Null";

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm widget-card">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <h5 class="card-title text-truncate m-0" title="${w.publicName}" style="max-width: 70%;">
                        ${w.publicName || 'Null'}
                    </h5>
                    <span class="badge bg-light text-dark border">${w.widgetType}</span>
                </div>
                
                <p class="card-text small text-muted mb-3">
                    ${window.translations.autor}: <strong>${w.authorName || 'Null'}</strong><br>
                    <span class="text-secondary">${dateStr}</span>
                </p>

                <div class="d-flex justify-content-between align-items-center mt-auto pt-2 border-top">
                    
                    <button class="btn btn-sm btn-link text-decoration-none p-0 me-2" 
                            onclick="window.toggleLike('${widgetId}')"
                            title="To se mi líbí">
                        <span class="${heartClass} fs-5 align-middle">${heartIcon}</span> 
                        <span class="text-dark fw-bold align-middle">${w.likesCount || 0}</span>
                    </button>

                    <div class="btn-group">
                        <button class="btn btn-sm btn-outline-secondary"
                                onclick='window.previewWidget("${w.widgetType}", ${dataString})'
                                title="Vyzkoušet nanečisto">
                            👁️ ${window.translations.preview}
                        </button>

                        <button class="btn btn-sm btn-success"
                                onclick='window.adoptWidget("${w.widgetType}", ${dataString})'
                                title="Přidat natrvalo na můj Dashboard">
                            ➕ ${window.translations.add}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    </div>
    `;
}

export function renderEmptyState() {
    return '<div class="col-12 text-muted fst-italic"></div>';
}