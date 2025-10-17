(function () {
    function setThemeIcon() {
        var icon = document.getElementById('themeIcon');
        if (!icon) return;
        // Koristimo dvije klase: bi-moon-stars (light prikaz) / bi-sun (dark prikaz)
        if (document.documentElement.classList.contains('theme-dark')) {
            icon.classList.remove('bi-moon-stars');
            icon.classList.add('bi-sun');
        } else {
            icon.classList.remove('bi-sun');
            icon.classList.add('bi-moon-stars');
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('themeToggle');
        if (btn) {
            btn.addEventListener('click', function () {
                document.documentElement.classList.toggle('theme-dark');
                try {
                    var isDark = document.documentElement.classList.contains('theme-dark');
                    localStorage.setItem('karlix-theme', isDark ? 'dark' : 'light');
                } catch { }
                setThemeIcon();
            });
            setThemeIcon();
        }
    });
})();
