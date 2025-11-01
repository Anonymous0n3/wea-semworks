document.addEventListener('DOMContentLoaded', function () {
    const authForms = document.getElementById('authForms');
    const logoutButton = document.getElementById('logoutButton');
    const token = localStorage.getItem('jwtToken');

    // ---------------- JWT Expiration & Auto Logout ----------------
    function getJwtExpiration(token) {
        if (!token) return null;
        try {
            const payload = JSON.parse(atob(token.split('.')[1]));
            return payload.exp ? payload.exp * 1000 : null;
        } catch (err) {
            console.error("Invalid JWT token", err);
            return null;
        }
    }

    function logoutUser() {
        localStorage.removeItem("jwtToken");
        if (authForms) authForms.style.display = 'block';
        if (logoutButton) logoutButton.style.display = 'none';
        window.location.href = "/";
    }

    if (logoutButton) {
        logoutButton.addEventListener('click', logoutUser);
    }

    function scheduleLogoutOnTokenExpiry() {
        const token = localStorage.getItem("jwtToken");
        const exp = getJwtExpiration(token);
        if (!exp) return;

        const now = Date.now();
        const timeout = exp - now;
        if (timeout <= 0) {
            logoutUser();
        } else {
            setTimeout(logoutUser, timeout);
        }
    }

    scheduleLogoutOnTokenExpiry();

    // ---------------- Inicializace ----------------
    if (token) {
        if (authForms) authForms.style.display = 'none';
        if (logoutButton) logoutButton.style.display = 'block';
    }

    // ---------------- Login ----------------
    const loginForm = document.getElementById("loginForm");
    if (loginForm) {
        loginForm.addEventListener("submit", async function (e) {
            e.preventDefault();
            const email = document.getElementById("loginEmail").value;
            const password = document.getElementById("loginPassword").value;
            const loginError = document.getElementById("loginError");

            try {
                const res = await fetch("/api/auth/login", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ email, password })
                });
                const data = await res.json();
                if (!res.ok) {
                    loginError.textContent = data?.message || "Login failed";
                    return;
                }

                localStorage.setItem("jwtToken", data.token);
                scheduleLogoutOnTokenExpiry();
                if (logoutButton) logoutButton.style.display = 'block';
                window.location.href = "/";
            } catch (err) {
                console.error(err);
                loginError.textContent = "Login failed";
            }
        });
    }

    // ---------------- Register ----------------
    const registerForm = document.getElementById("registerForm");
    if (registerForm) {
        registerForm.addEventListener("submit", async function (e) {
            e.preventDefault();
            const name = document.getElementById("regName").value;
            const email = document.getElementById("regEmail").value;
            const password = document.getElementById("regPassword").value;
            const registerError = document.getElementById("registerError");

            try {
                const res = await fetch("/api/auth/register", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ name, email, password })
                });
                const data = await res.json();
                if (!res.ok) {
                    registerError.textContent = data?.message || "Registration failed";
                    return;
                }

                // auto-login
                const loginRes = await fetch("/api/auth/login", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ email, password })
                });
                const loginData = await loginRes.json();
                if (!loginRes.ok) {
                    registerError.textContent = loginData?.message || "Auto login failed";
                    return;
                }

                localStorage.setItem("jwtToken", loginData.token);
                scheduleLogoutOnTokenExpiry();
                if (logoutButton) logoutButton.style.display = 'block';
                if (authForms) authForms.style.display = 'none';
                window.location.href = "/";
            } catch (err) {
                console.error(err);
                registerError.textContent = "Registration or login failed";
            }
        });
    }

    // ---------------- Google Login ----------------
    const googleBtn = document.getElementById("googleLoginBtn");
    if (googleBtn) {
        googleBtn.addEventListener("click", () => {
            const clientId = document.querySelector("meta[name='google-client-id']")?.content;
            const redirectUri = document.querySelector("meta[name='google-redirect-uri']")?.content;
            const scope = encodeURIComponent("openid email profile");
            const authUrl =
                `https://accounts.google.com/o/oauth2/v2/auth?client_id=${clientId}` +
                `&redirect_uri=${encodeURIComponent(redirectUri)}` +
                `&response_type=code&scope=${scope}&access_type=offline&prompt=select_account`;
            window.location.href = authUrl;
        });
    }

    // ---------------- Po návratu z Google OAuth ----------------
    const params = new URLSearchParams(window.location.search);
    const code = params.get("code");
    if (code) {
        (async () => {
            try {
                const res = await fetch("/api/auth/google/exchange", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ code })
                });
                const data = await res.json();
                if (!res.ok) throw new Error(data?.message || "Google login failed");

                localStorage.setItem("jwtToken", data.token);
                scheduleLogoutOnTokenExpiry();
                if (logoutButton) logoutButton.style.display = 'block';
                window.history.replaceState({}, document.title, window.location.pathname);
                window.location.href = "/";
            } catch (err) {
                console.error("Google login failed:", err);
                alert("Google login failed: " + err.message);
            }
        })();
    }
});
