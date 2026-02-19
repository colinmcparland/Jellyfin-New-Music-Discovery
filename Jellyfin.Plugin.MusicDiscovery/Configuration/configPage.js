(function () {
    'use strict';
    var pluginId = 'a3b9c2d1-e4f5-6789-abcd-ef0123456789';

    document.querySelector('#musicDiscoveryConfigPage')
        .addEventListener('pageshow', function () {
            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                document.querySelector('#txtLastFmApiKey').value = config.LastFmApiKey || '';
                document.querySelector('#selMaxResults').value = config.MaxRecommendations || 8;
                document.querySelector('#txtCacheDuration').value = config.CacheDurationMinutes || 30;
                document.querySelector('#chkEnableArtists').checked = config.EnableForArtists;
                document.querySelector('#chkEnableAlbums').checked = config.EnableForAlbums;
                document.querySelector('#chkEnableTracks').checked = config.EnableForTracks;
                Dashboard.hideLoadingMsg();
            });
        });

    document.querySelector('#musicDiscoveryConfigForm')
        .addEventListener('submit', function (e) {
            e.preventDefault();
            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                config.LastFmApiKey = document.querySelector('#txtLastFmApiKey').value.trim();
                config.MaxRecommendations = parseInt(document.querySelector('#selMaxResults').value, 10);
                config.CacheDurationMinutes = parseInt(document.querySelector('#txtCacheDuration').value, 10);
                config.EnableForArtists = document.querySelector('#chkEnableArtists').checked;
                config.EnableForAlbums = document.querySelector('#chkEnableAlbums').checked;
                config.EnableForTracks = document.querySelector('#chkEnableTracks').checked;
                ApiClient.updatePluginConfiguration(pluginId, config)
                    .then(Dashboard.processPluginConfigurationUpdateResult);
            });
            return false;
        });
})();
