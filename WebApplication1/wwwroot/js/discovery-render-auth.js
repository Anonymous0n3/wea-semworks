// discovery-render-auth.js

export function renderCard(w, userEmail) {
    // 1. DEFINITIVNÍ ZÍSKÁNÍ ID (zkusí všechny možnosti)
    // Server (CouchDB) posílá '_id', ale C# někdy serializuje 'Id'. Toto pokryje vše.
    const widgetId = w._id || w.id || w.Id;

    // 2. DIAGNOSTIKA (Pokud otevřeš konzoli F12, uvidíš to tam)
    if (!widgetId) {
        console.error("❌ POZOR: Widget nemá ID! Data widgetu:", w);
        return `<div class="col-md-4"><div class="alert alert-danger">Chyba: Widget bez ID</div></div>`;
    }

    // Zbytek logiky pro barvy a ikony...
    const isLiked = w.likedBy && w.likedBy.includes(userEmail);
    const heartClass = isLiked ? "text-danger" : "text-muted";
    const heartIcon = isLiked ? "❤️" : "🤍";

    // Formátování data
    const dateStr = w.createdAt ? new Date(w.createdAt).toLocaleDateString() : "Neznámé datum";

    // 3. HTML (Všimni si použití proměnné widgetId v onclicku)
    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm widget-card">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-start">
                    <h5 class="card-title text-truncate" title="${w.publicName}">${w.publicName || 'Bezejmenný'}</h5>
                    <span class="badge bg-light text-dark border">${w.widgetType}</span>
                </div>
                
                <p class="card-text small text-muted mb-2">
                    Autor: <strong>${w.authorName || 'Neznámý'}</strong><br>
                    ${dateStr}
                </p>

                <div class="d-flex justify-content-between align-items-center mt-3">
                    
                    <button class="btn btn-sm btn-link text-decoration-none p-0" 
                            onclick="window.toggleLike('${widgetId}')">
                        <span class="${heartClass} fs-5">${heartIcon}</span> 
                        <span class="text-dark fw-bold">${w.likesCount || 0}</span>
                    </button>

                    <button class="btn btn-sm btn-outline-primary"
                            onclick='window.previewWidget("${w.widgetType}", ${JSON.stringify(w.widgetData || {})})'>
                        Použít
                    </button>
                </div>
            </div>
        </div>
    </div>
    `;
}

export function renderEmptyState() {
    return '<div class="col-12 text-muted fst-italic">Zatím nemáte žádné oblíbené widgety.</div>';
}