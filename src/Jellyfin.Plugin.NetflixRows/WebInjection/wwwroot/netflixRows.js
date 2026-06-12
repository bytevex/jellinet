// NetflixRows web injection script.
// Loaded by index.html (injected by WebInjectionEntryPoint.cs).
// Renders the genre rows configured in the NetflixRows plugin on the Jellyfin home page.
(function () {
    var API_BASE = '/NetflixRows';
    var CONTAINER_CLASS = 'netflixRows-section';

    function getApiClient() {
        return window.ApiClient;
    }

    function authFetch(url) {
        var apiClient = getApiClient();
        if (!apiClient) {
            return Promise.reject(new Error('ApiClient not available'));
        }

        return apiClient.fetch({
            url: apiClient.getUrl(url),
            type: 'GET',
            dataType: 'json'
        });
    }

    function findHomeSectionsContainer() {
        return document.querySelector('.homeSectionsContainer')
            || document.querySelector('#indexPage:not(.hide) .sections')
            || document.querySelector('.homePage:not(.hide) .sections');
    }

    function buildCard(item, apiClient) {
        var card = document.createElement('a');
        card.className = 'netflixRows-card';
        card.href = '#/details?id=' + item.Id + '&serverId=' + apiClient.serverId();

        var imageTag = item.ImageTags && item.ImageTags.Primary;
        if (imageTag) {
            var imgUrl = apiClient.getScaledImageUrl(item.Id, {
                type: 'Primary',
                tag: imageTag,
                maxWidth: 300
            });
            var img = document.createElement('img');
            img.src = imgUrl;
            img.loading = 'lazy';
            img.alt = item.Name || '';
            card.appendChild(img);
        } else {
            card.classList.add('netflixRows-card--noimage');
            card.textContent = item.Name || '';
        }

        return card;
    }

    function buildRow(rowSummary, apiClient) {
        return authFetch(API_BASE + '/Rows/' + rowSummary.Id + '/Items')
            .then(function (items) {
                if (!items || items.length === 0) {
                    return null;
                }

                var section = document.createElement('div');
                section.className = CONTAINER_CLASS + ' verticalSection';
                section.dataset.netflixRowId = rowSummary.Id;

                var title = document.createElement('h2');
                title.className = 'netflixRows-title sectionTitle';
                title.textContent = rowSummary.Title;
                section.appendChild(title);

                var scroller = document.createElement('div');
                scroller.className = 'netflixRows-scroller';

                items.forEach(function (item) {
                    scroller.appendChild(buildCard(item, apiClient));
                });

                section.appendChild(scroller);
                return section;
            })
            .catch(function (err) {
                console.error('NetflixRows: failed to load items for row "' + rowSummary.Title + '"', err);
                return null;
            });
    }

    function removeExistingRows(container) {
        container.querySelectorAll('.' + CONTAINER_CLASS).forEach(function (el) {
            el.remove();
        });
    }

    function injectRows() {
        var container = findHomeSectionsContainer();
        if (!container) {
            return;
        }

        var apiClient = getApiClient();
        if (!apiClient || !apiClient.isLoggedIn || !apiClient.isLoggedIn()) {
            return;
        }

        authFetch(API_BASE + '/Rows')
            .then(function (rows) {
                removeExistingRows(container);

                var promises = rows.map(function (row) {
                    return buildRow(row, apiClient);
                });

                return Promise.all(promises);
            })
            .then(function (sections) {
                sections.forEach(function (section) {
                    if (section) {
                        container.appendChild(section);
                    }
                });
            })
            .catch(function (err) {
                console.error('NetflixRows: failed to load row list', err);
            });
    }

    var debounceTimer = null;
    function scheduleInjection(delay) {
        if (debounceTimer) {
            clearTimeout(debounceTimer);
        }

        debounceTimer = setTimeout(injectRows, delay || 800);
    }

    function isHomePath() {
        var hash = window.location.hash || '';
        return hash === '#/' || hash === '' || hash.indexOf('#/home') === 0;
    }

    function onNavigate() {
        if (isHomePath()) {
            scheduleInjection(1000);
        }
    }

    document.addEventListener('viewshow', onNavigate);
    window.addEventListener('hashchange', onNavigate);

    // Initial load.
    if (isHomePath()) {
        scheduleInjection(1500);
    }
})();
