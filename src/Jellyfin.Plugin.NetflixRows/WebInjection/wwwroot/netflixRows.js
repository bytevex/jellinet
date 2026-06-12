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

    function addScrollNav(section, scroller) {
        var prevButton = document.createElement('button');
        prevButton.type = 'button';
        prevButton.className = 'netflixRows-nav netflixRows-nav--prev';
        prevButton.setAttribute('aria-label', 'Scroll left');
        prevButton.innerHTML = '&#10094;';

        var nextButton = document.createElement('button');
        nextButton.type = 'button';
        nextButton.className = 'netflixRows-nav netflixRows-nav--next';
        nextButton.setAttribute('aria-label', 'Scroll right');
        nextButton.innerHTML = '&#10095;';

        function scrollByPage(direction) {
            var amount = scroller.clientWidth * 0.9 * direction;
            scroller.scrollBy({ left: amount, behavior: 'smooth' });
        }

        prevButton.addEventListener('click', function () {
            scrollByPage(-1);
        });

        nextButton.addEventListener('click', function () {
            scrollByPage(1);
        });

        // Allow the mouse wheel (and trackpad vertical scroll) to scroll the row horizontally.
        scroller.addEventListener('wheel', function (e) {
            if (e.deltaY === 0) {
                return;
            }

            e.preventDefault();
            scroller.scrollBy({ left: e.deltaY });
        }, { passive: false });

        section.appendChild(prevButton);
        section.appendChild(nextButton);
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
                addScrollNav(section, scroller);
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

    function normalizeTitle(text) {
        return String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    }

    function hideConfiguredSections(container, hiddenTitles) {
        if (!hiddenTitles || hiddenTitles.length === 0) {
            return;
        }

        var normalizedHidden = hiddenTitles.map(normalizeTitle).filter(function (t) {
            return t.length > 0;
        });

        if (normalizedHidden.length === 0) {
            return;
        }

        Array.prototype.forEach.call(container.children, function (section) {
            if (section.classList.contains(CONTAINER_CLASS)) {
                // Never hide our own rows.
                return;
            }

            var heading = section.querySelector('.sectionTitle, h2');
            if (!heading) {
                return;
            }

            var sectionTitle = normalizeTitle(heading.textContent);
            if (normalizedHidden.indexOf(sectionTitle) !== -1) {
                section.style.display = 'none';
            }
        });
    }

    var injectionToken = 0;

    function injectRows() {
        var container = findHomeSectionsContainer();
        if (!container) {
            return;
        }

        var apiClient = getApiClient();
        if (!apiClient || !apiClient.isLoggedIn || !apiClient.isLoggedIn()) {
            return;
        }

        var token = ++injectionToken;

        Promise.all([
            authFetch(API_BASE + '/Rows'),
            authFetch(API_BASE + '/HiddenSections').catch(function () { return []; })
        ])
            .then(function (results) {
                var rows = results[0] || [];
                var hiddenTitles = results[1] || [];

                return Promise.all(rows.map(function (row) {
                    return buildRow(row, apiClient);
                })).then(function (sections) {
                    return { sections: sections, hiddenTitles: hiddenTitles };
                });
            })
            .then(function (result) {
                // If a newer injection pass has started in the meantime, discard
                // this (stale) result instead of appending duplicate rows.
                if (token !== injectionToken) {
                    return;
                }

                removeExistingRows(container);

                result.sections.forEach(function (section) {
                    if (section) {
                        container.appendChild(section);
                    }
                });

                hideConfiguredSections(container, result.hiddenTitles);
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
