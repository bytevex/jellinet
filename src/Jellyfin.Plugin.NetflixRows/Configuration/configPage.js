var PluginId = 'a1b2c3d4-e5f6-4789-a012-b3c4d5e6f7a8';

    var MediaTypeOptions = [
        { value: 0, label: 'Films' },
        { value: 1, label: 'Series' },
        { value: 2, label: 'Films + Series' }
    ];

    var SortOrderOptions = [
        { value: 0, label: 'Nieuwste eerst' },
        { value: 1, label: 'Willekeurig' },
        { value: 2, label: 'Beoordeling' },
        { value: 3, label: 'Titel (A-Z)' }
    ];

    function optionsHtml(options, selected) {
        return options.map(function (o) {
            var sel = o.value === selected ? ' selected' : '';
            return '<option value="' + o.value + '"' + sel + '>' + o.label + '</option>';
        }).join('');
    }

    function rowTemplate(row, index, genreList) {
        var genreOptions = '<datalist id="genreList' + index + '">' +
            genreList.map(function (g) { return '<option value="' + g + '"></option>'; }).join('') +
            '</datalist>';

        return '' +
            '<div class="netflixRows-row" data-index="' + index + '" style="border:1px solid rgba(255,255,255,.15); border-radius:6px; padding:1em; margin-bottom:1em;">' +
                '<div class="inputContainer">' +
                    '<label class="inputLabel" for="title' + index + '">Titel (incl. emoji)</label>' +
                    '<input is="emby-input" type="text" id="title' + index + '" class="row-title" value="' + escapeHtml(row.Title) + '" />' +
                '</div>' +
                '<div class="inputContainer">' +
                    '<label class="inputLabel" for="genres' + index + '">Genres (komma-gescheiden, gebruikt OR)</label>' +
                    '<input is="emby-input" type="text" id="genres' + index + '" class="row-genres" list="genreList' + index + '" value="' + escapeHtml((row.Genres || []).join(', ')) + '" />' +
                    genreOptions +
                '</div>' +
                '<div class="inputContainer">' +
                    '<label class="selectLabel" for="mediaType' + index + '">Type</label>' +
                    '<select is="emby-select" id="mediaType' + index + '" class="row-mediatype">' + optionsHtml(MediaTypeOptions, row.MediaType) + '</select>' +
                '</div>' +
                '<div class="inputContainer">' +
                    '<label class="selectLabel" for="sortOrder' + index + '">Sortering</label>' +
                    '<select is="emby-select" id="sortOrder' + index + '" class="row-sortorder">' + optionsHtml(SortOrderOptions, row.SortOrder) + '</select>' +
                '</div>' +
                '<div class="inputContainer" style="display:flex; gap:1em;">' +
                    '<div style="flex:1;">' +
                        '<label class="inputLabel" for="minItems' + index + '">Min. items om rij te tonen</label>' +
                        '<input is="emby-input" type="number" min="0" id="minItems' + index + '" class="row-minitems" value="' + row.MinItems + '" />' +
                    '</div>' +
                    '<div style="flex:1;">' +
                        '<label class="inputLabel" for="maxItems' + index + '">Max. items in rij</label>' +
                        '<input is="emby-input" type="number" min="1" id="maxItems' + index + '" class="row-maxitems" value="' + row.MaxItems + '" />' +
                    '</div>' +
                '</div>' +
                '<label style="display:block; margin-top:.5em;">' +
                    '<input type="checkbox" is="emby-checkbox" class="row-enabled"' + (row.Enabled ? ' checked' : '') + ' />' +
                    '<span>Rij ingeschakeld</span>' +
                '</label>' +
                '<button is="emby-button" type="button" class="raised row-remove" style="margin-top:.5em;"><span>Verwijder rij</span></button>' +
            '</div>';
    }

    function escapeHtml(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function readRowFromDom(rowEl) {
        return {
            Id: rowEl.dataset.rowId,
            Title: rowEl.querySelector('.row-title').value,
            Genres: rowEl.querySelector('.row-genres').value
                .split(',')
                .map(function (g) { return g.trim(); })
                .filter(function (g) { return g.length > 0; }),
            MediaType: parseInt(rowEl.querySelector('.row-mediatype').value, 10),
            SortOrder: parseInt(rowEl.querySelector('.row-sortorder').value, 10),
            MinItems: parseInt(rowEl.querySelector('.row-minitems').value, 10) || 0,
            MaxItems: parseInt(rowEl.querySelector('.row-maxitems').value, 10) || 1,
            Enabled: rowEl.querySelector('.row-enabled').checked
        };
    }

    function generateGuid() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0;
            var v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

export default class NetflixRowsConfigPage {
    constructor(view) {
        var rowsContainer = view.querySelector('#RowsContainer');
        var addRowButton = view.querySelector('#AddRowButton');
        var libraryContainer = view.querySelector('#LibraryCheckboxes');
        var form = view.querySelector('#NetflixRowsConfigForm');

        var currentRows = [];
        var availableGenres = [];
        var availableLibraries = [];

        function renderRows() {
            rowsContainer.innerHTML = currentRows.map(function (row, index) {
                return rowTemplate(row, index, availableGenres);
            }).join('');

            Array.prototype.forEach.call(rowsContainer.querySelectorAll('.netflixRows-row'), function (rowEl, index) {
                rowEl.dataset.rowId = currentRows[index].Id;
                rowEl.querySelector('.row-remove').addEventListener('click', function () {
                    var idx = parseInt(rowEl.dataset.index, 10);
                    currentRows.splice(idx, 1);
                    renderRows();
                });
            });
        }

        function renderLibraries(selectedIds) {
            libraryContainer.innerHTML = availableLibraries.map(function (lib) {
                var checked = selectedIds.indexOf(lib.Id) !== -1 ? ' checked' : '';
                return '<label style="display:block;">' +
                    '<input type="checkbox" is="emby-checkbox" class="library-checkbox" data-id="' + lib.Id + '"' + checked + ' />' +
                    '<span>' + escapeHtml(lib.Name) + '</span>' +
                    '</label>';
            }).join('');
        }

        function loadData() {
            Dashboard.showLoadingMsg();

            var apiClient = ApiClient;

            Promise.all([
                apiClient.getPluginConfiguration(PluginId),
                apiClient.getJSON(apiClient.getUrl('NetflixRows/Genres')).catch(function () { return []; }),
                apiClient.getJSON(apiClient.getUrl('NetflixRows/Libraries')).catch(function () { return []; })
            ]).then(function (results) {
                var config = results[0];
                availableGenres = results[1] || [];
                availableLibraries = results[2] || [];

                currentRows = (config.Rows || []).map(function (r) {
                    return Object.assign({}, r, { Id: r.Id || generateGuid() });
                });

                view.querySelector('#EnableHomeScreenSectionsIntegration').checked = !!config.EnableHomeScreenSectionsIntegration;
                view.querySelector('#EnableWebInjection').checked = !!config.EnableWebInjection;

                renderRows();
                renderLibraries(config.LibraryFolderIds || []);

                Dashboard.hideLoadingMsg();
            });
        }

        function saveData() {
            Dashboard.showLoadingMsg();

            ApiClient.getPluginConfiguration(PluginId).then(function (config) {
                config.Rows = Array.prototype.map.call(rowsContainer.querySelectorAll('.netflixRows-row'), readRowFromDom);

                config.LibraryFolderIds = Array.prototype.map.call(
                    libraryContainer.querySelectorAll('.library-checkbox:checked'),
                    function (el) { return el.dataset.id; }
                );

                config.EnableHomeScreenSectionsIntegration = view.querySelector('#EnableHomeScreenSectionsIntegration').checked;
                config.EnableWebInjection = view.querySelector('#EnableWebInjection').checked;

                ApiClient.updatePluginConfiguration(PluginId, config).then(function (result) {
                    Dashboard.processPluginConfigurationUpdateResult(result);
                });
            });
        }

        addRowButton.addEventListener('click', function () {
            currentRows.push({
                Id: generateGuid(),
                Title: '🎬 Nieuwe rij',
                Genres: [],
                MediaType: 2,
                SortOrder: 0,
                MinItems: 5,
                MaxItems: 24,
                Enabled: true
            });
            renderRows();
        });

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            saveData();
            return false;
        });

        view.addEventListener('viewshow', loadData);

        loadData();
    }
}
