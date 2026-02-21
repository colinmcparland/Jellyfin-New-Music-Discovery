export default function (view) {
    'use strict';

    // Ensure CSS is loaded
    var cssLink = document.querySelector('link[href*="MusicDiscoveryCSS"]');
    if (!cssLink) {
        cssLink = document.createElement('link');
        cssLink.rel = 'stylesheet';
        cssLink.href = 'configurationpage?name=MusicDiscoveryCSS';
        document.head.appendChild(cssLink);
    }

    view.addEventListener('viewshow', function () {
        loadSaved();
    });

    function loadSaved() {
        var grid = view.querySelector('#savedGrid');
        var empty = view.querySelector('#savedEmpty');
        if (!grid) return;

        grid.innerHTML = '';

        var url = ApiClient.getUrl('MusicDiscovery/Saved');
        ApiClient.getJSON(url).then(function (data) {
            if (!data || !data.Items || data.Items.length === 0) {
                empty.style.display = '';
                return;
            }

            empty.style.display = 'none';

            data.Items.forEach(function (item) {
                grid.appendChild(createSavedCard(item));
            });
        });
    }

    function createSavedCard(item) {
        var card = document.createElement('div');
        card.className = 'card overflowSquareCard card-hoverable md-saved-card';

        var cardBox = document.createElement('div');
        cardBox.className = 'cardBox cardBox-bottompadded';

        var cardScalable = document.createElement('div');
        cardScalable.className = 'cardScalable';

        var padder = document.createElement('div');
        padder.className = 'cardPadder cardPadder-overflowSquare';

        if (!item.ImageUrl) {
            var icon = document.createElement('span');
            icon.className = 'material-icons cardImageIcon';
            icon.textContent = item.Type === 'artist' ? 'person' : 'album';
            icon.setAttribute('aria-hidden', 'true');
            padder.appendChild(icon);
        }

        var imgContainer = document.createElement('div');
        imgContainer.className = 'cardImageContainer coveredImage cardContent';
        if (item.ImageUrl) {
            imgContainer.style.backgroundImage = 'url("' + item.ImageUrl + '")';
        }

        cardScalable.appendChild(padder);
        cardScalable.appendChild(imgContainer);

        // Overlay — same native structure as recommendation tiles
        var overlayContainer = document.createElement('div');
        overlayContainer.className = 'cardOverlayContainer';

        var brContainer = document.createElement('div');
        brContainer.className = 'cardOverlayButton-br flex';

        var bookmarkBtn = document.createElement('button');
        bookmarkBtn.className = 'cardOverlayButton cardOverlayButton-hover md-bookmark-btn md-saved';
        bookmarkBtn.setAttribute('title', 'Remove saved recommendation');
        var bookmarkIcon = document.createElement('span');
        bookmarkIcon.className = 'material-icons cardOverlayButtonIcon cardOverlayButtonIcon-hover';
        bookmarkIcon.textContent = 'bookmark';
        bookmarkIcon.setAttribute('aria-hidden', 'true');
        bookmarkBtn.appendChild(bookmarkIcon);

        bookmarkBtn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            deleteSaved(item, card);
        });

        brContainer.appendChild(bookmarkBtn);
        overlayContainer.appendChild(brContainer);
        cardScalable.appendChild(overlayContainer);
        cardBox.appendChild(cardScalable);

        // Name text
        var nameText = document.createElement('div');
        nameText.className = 'cardText cardTextCentered cardText-first';
        var nameBdi = document.createElement('bdi');
        if (item.LastFmUrl) {
            var nameLink = document.createElement('a');
            nameLink.className = 'textActionButton';
            nameLink.href = item.LastFmUrl;
            nameLink.target = '_blank';
            nameLink.rel = 'noopener noreferrer';
            nameLink.title = item.Name;
            nameLink.textContent = item.Name;
            nameBdi.appendChild(nameLink);
        } else {
            nameBdi.textContent = item.Name;
        }
        nameText.appendChild(nameBdi);
        cardBox.appendChild(nameText);

        // Artist text
        if (item.ArtistName && item.Type !== 'artist') {
            var artistText = document.createElement('div');
            artistText.className = 'cardText cardText-secondary cardTextCentered';
            var artistBdi = document.createElement('bdi');
            artistBdi.textContent = item.ArtistName;
            artistText.appendChild(artistBdi);
            cardBox.appendChild(artistText);
        }

        // Type badge
        var typeText = document.createElement('div');
        typeText.className = 'cardText cardText-secondary cardTextCentered';
        typeText.style.textTransform = 'capitalize';
        typeText.style.opacity = '0.6';
        typeText.textContent = item.Type;
        cardBox.appendChild(typeText);

        card.appendChild(cardBox);
        return card;
    }

    function deleteSaved(item, cardElement) {
        var url = ApiClient.getUrl('MusicDiscovery/Saved');
        ApiClient.ajax({
            type: 'DELETE', url: url,
            contentType: 'application/json',
            data: JSON.stringify({
                Name: item.Name, ArtistName: item.ArtistName, Type: item.Type
            })
        }).then(function () {
            cardElement.remove();
            var grid = view.querySelector('#savedGrid');
            var empty = view.querySelector('#savedEmpty');
            if (grid && grid.children.length === 0 && empty) {
                empty.style.display = '';
            }
        });
    }
}
