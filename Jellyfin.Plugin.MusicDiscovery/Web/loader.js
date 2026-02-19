(function () {
    'use strict';

    // Check if the main script is already loaded
    if (window.__musicDiscoveryLoaded) return;
    window.__musicDiscoveryLoaded = true;

    // Load the main discovery panel script
    var script = document.createElement('script');
    script.src = 'configurationpage?name=MusicDiscoveryJS';
    document.head.appendChild(script);
})();
