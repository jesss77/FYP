// Theme Toggle Script
(function() {
    'use strict';

    const THEME_KEY = 'preferred-theme';
    const THEME_ATTR = 'data-theme';
    
    // Get stored theme or detect system preference
    function getStoredTheme() {
        const stored = localStorage.getItem(THEME_KEY);
        if (stored) {
            return stored;
        }
        
        // Detect system preference
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            return 'dark';
        }
        
        return 'light';
    }
    
    // Apply theme to document
    function applyTheme(theme) {
        document.documentElement.setAttribute(THEME_ATTR, theme);
        localStorage.setItem(THEME_KEY, theme);
        
        // Update toggle button icon
        updateToggleIcon(theme);
    }
    
    // Update toggle button icon
    function updateToggleIcon(theme) {
        const toggle = document.querySelector('.theme-toggle');
        if (!toggle) return;
        
        const sunIcon = toggle.querySelector('.theme-toggle-sun');
        const moonIcon = toggle.querySelector('.theme-toggle-moon');
        
        if (theme === 'dark') {
            if (sunIcon) sunIcon.style.display = 'block';
            if (moonIcon) moonIcon.style.display = 'none';
        } else {
            if (sunIcon) sunIcon.style.display = 'none';
            if (moonIcon) moonIcon.style.display = 'block';
        }
    }
    
    // Toggle theme
    function toggleTheme() {
        const currentTheme = document.documentElement.getAttribute(THEME_ATTR) || 'light';
        const newTheme = currentTheme === 'light' ? 'dark' : 'light';
        
        // Add transitioning class to prevent animation flash
        document.documentElement.classList.add('theme-transitioning');
        
        applyTheme(newTheme);
        
        // Remove transitioning class after a short delay
        setTimeout(() => {
            document.documentElement.classList.remove('theme-transitioning');
        }, 300);
    }
    
    // Initialize theme on page load
    function initTheme() {
        const theme = getStoredTheme();
        applyTheme(theme);
        
        // Add event listener to toggle button
        const toggle = document.querySelector('.theme-toggle');
        if (toggle) {
            toggle.addEventListener('click', toggleTheme);
        }
    }
    
    // Listen for system theme changes
    if (window.matchMedia) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            // Only auto-switch if user hasn't manually set a preference
            if (!localStorage.getItem(THEME_KEY)) {
                applyTheme(e.matches ? 'dark' : 'light');
            }
        });
    }
    
    // Initialize immediately
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initTheme);
    } else {
        initTheme();
    }
    
    // Export for manual control if needed
    window.themeController = {
        toggle: toggleTheme,
        set: applyTheme,
        get: () => document.documentElement.getAttribute(THEME_ATTR) || 'light'
    };
})();
