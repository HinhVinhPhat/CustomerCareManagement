// Shared behaviour for every page: the sidebar drawer, the toast helper, the notification
// panel and, on the sign-in page, the demo-account buttons and the show-password toggle.
(function () {
    'use strict';

    function toast(message, type) {
        var element = document.createElement('div');
        element.className = 'toast toast-' + (type || 'info');
        element.setAttribute('role', 'status');
        element.textContent = message;
        document.body.appendChild(element);
        window.setTimeout(function () { element.remove(); }, 3500);
    }

    window.ccmToast = toast;

    document.addEventListener('DOMContentLoaded', function () {
        // The result of the last action, rendered by _ActionMessage as a marker: a success becomes a
        // floating toast. A failure is not marked and stays on screen as an alert block instead.
        document.querySelectorAll('[data-toast]').forEach(function (element) {
            toast(element.getAttribute('data-toast-message') || '',
                element.getAttribute('data-toast-type') || 'info');
            element.remove();
        });

        // Sidebar drawer on small screens.
        var side = document.getElementById('side');
        var menuBtn = document.getElementById('menuBtn');
        if (side && menuBtn) {
            menuBtn.addEventListener('click', function () { side.classList.toggle('-translate-x-full'); });
        }

        // Pages that belong to a module which is not built yet say so instead of failing. The
        // sentence comes from the markup (a resource key rendered by the view), never from here,
        // so it is already in the language the user chose.
        document.querySelectorAll('[data-soon="true"]').forEach(function (element) {
            var eventName = element.tagName === 'FORM' ? 'submit' : 'click';
            element.addEventListener(eventName, function (event) {
                event.preventDefault();
                toast(element.getAttribute('data-soon-message') || '');
            });
        });

        // Notification panel.
        var bell = document.getElementById('bell');
        var bellBtn = document.getElementById('bellBtn');
        var bellPanel = document.getElementById('bellPanel');
        if (bell && bellBtn && bellPanel) {
            bellBtn.addEventListener('click', function () {
                bellPanel.classList.toggle('hidden');
                bellBtn.setAttribute('aria-expanded', String(!bellPanel.classList.contains('hidden')));
            });
            document.addEventListener('click', function (event) {
                if (!bell.contains(event.target)) { bellPanel.classList.add('hidden'); }
            });
        }

        // Quick search: suggestions for customers, fetched from /customers/suggest.
        var searchBox = document.getElementById('searchBox');
        var searchSuggestions = document.getElementById('searchSuggestions');
        if (searchBox && searchSuggestions) {
            var debounceTimer = null;
            var activeIndex = -1;
            var currentItems = [];

            var renderSuggestions = function (items) {
                currentItems = items || [];
                activeIndex = -1;
                searchSuggestions.innerHTML = '';
                if (currentItems.length === 0) {
                    var empty = document.createElement('div');
                    empty.className = 'suggest-empty';
                    empty.textContent = searchBox.getAttribute('data-empty') || '';
                    searchSuggestions.appendChild(empty);
                    searchSuggestions.classList.remove('hidden');
                    return;
                }
                var list = document.createElement('ul');
                list.className = 'suggest-list';
                currentItems.forEach(function (item, index) {
                    var li = document.createElement('li');
                    li.className = 'suggest-item';
                    li.setAttribute('role', 'option');
                    li.setAttribute('data-index', String(index));
                    var name = document.createElement('span');
                    name.className = 'suggest-name';
                    name.textContent = item.customerName;
                    var type = document.createElement('span');
                    type.className = 'suggest-type';
                    type.textContent = item.customerTypeLabel || item.customerType;
                    li.appendChild(name);
                    li.appendChild(type);
                    li.addEventListener('click', function () {
                        window.location.href = '/customers/' + item.customerId;
                    });
                    list.appendChild(li);
                });
                searchSuggestions.appendChild(list);
                searchSuggestions.classList.remove('hidden');
            };

            var hideSuggestions = function () {
                searchSuggestions.classList.add('hidden');
                searchSuggestions.innerHTML = '';
                currentItems = [];
                activeIndex = -1;
            };

            var fetchSuggestions = function (term) {
                if (term.length < 2) { hideSuggestions(); return; }
                var url = '/customers/suggest?q=' + encodeURIComponent(term);
                fetch(url, { headers: { 'Accept': 'application/json' } })
                    .then(function (response) { return response.ok ? response.json() : []; })
                    .then(function (data) { renderSuggestions(Array.isArray(data) ? data : []); })
                    .catch(function () { hideSuggestions(); });
            };

            searchBox.addEventListener('input', function () {
                var term = searchBox.value.trim();
                if (debounceTimer) { window.clearTimeout(debounceTimer); }
                debounceTimer = window.setTimeout(function () { fetchSuggestions(term); }, 250);
            });

            searchBox.addEventListener('keydown', function (event) {
                if (event.key === 'Escape') { hideSuggestions(); return; }
                if (currentItems.length === 0) { return; }
                if (event.key === 'ArrowDown') {
                    event.preventDefault();
                    activeIndex = Math.min(activeIndex + 1, currentItems.length - 1);
                    highlightActive();
                } else if (event.key === 'ArrowUp') {
                    event.preventDefault();
                    activeIndex = Math.max(activeIndex - 1, 0);
                    highlightActive();
                } else if (event.key === 'Enter' && activeIndex >= 0) {
                    event.preventDefault();
                    window.location.href = '/customers/' + currentItems[activeIndex].customerId;
                }
            });

            function highlightActive() {
                var nodes = searchSuggestions.querySelectorAll('.suggest-item');
                nodes.forEach(function (node, index) {
                    node.classList.toggle('active', index === activeIndex);
                });
            }

            document.addEventListener('click', function (event) {
                if (!searchBox.contains(event.target) && !searchSuggestions.contains(event.target)) {
                    hideSuggestions();
                }
            });
        }

        // Sign-in page: demo accounts and the show-password toggle.
        var demo = document.getElementById('demoAccounts');
        if (demo) {
            demo.addEventListener('click', function (event) {
                var button = event.target.closest('[data-demo-user]');
                if (!button) { return; }
                var username = document.getElementById('Username');
                var password = document.getElementById('Password');
                if (username) { username.value = button.getAttribute('data-demo-user'); }
                if (password) { password.value = '123456'; }
            });
        }

        var eye = document.getElementById('eye');
        if (eye) {
            eye.addEventListener('click', function () {
                var password = document.getElementById('Password');
                if (password) { password.type = password.type === 'password' ? 'text' : 'password'; }
            });
        }

        // A dangerous action asks before it happens. A form that carries data-confirm opens the
        // shared dialog instead of posting; the button inside the dialog posts it. The sentences
        // come from the markup, so they are already in the language the user chose. Without
        // JavaScript the form posts straight away - the permission check is on the server either way.
        var confirmDialog = document.getElementById('confirmDialog');
        if (confirmDialog) {
            var pendingForm = null;
            var titleBox = confirmDialog.querySelector('[data-confirm-title]');
            var bodyBox = confirmDialog.querySelector('[data-confirm-body]');
            var reasonWrap = confirmDialog.querySelector('[data-confirm-reason-wrap]');
            var reasonBox = confirmDialog.querySelector('[data-confirm-reason-input]');

            document.querySelectorAll('form[data-confirm]').forEach(function (form) {
                form.addEventListener('submit', function (event) {
                    if (form.dataset.confirmAccepted === '1') { return; }
                    event.preventDefault();
                    pendingForm = form;
                    if (titleBox) { titleBox.textContent = form.getAttribute('data-confirm-title') || ''; }
                    if (bodyBox) { bodyBox.textContent = form.getAttribute('data-confirm-body') || ''; }
                    // Only a form that asks for a reason (data-confirm-reason) shows the textarea.
                    var wantsReason = form.hasAttribute('data-confirm-reason');
                    if (reasonWrap) { reasonWrap.classList.toggle('hidden', !wantsReason); }
                    if (reasonBox) { reasonBox.value = ''; }
                    confirmDialog.showModal();
                });
            });

            var cancelBtn = confirmDialog.querySelector('[data-confirm-cancel]');
            if (cancelBtn) {
                cancelBtn.addEventListener('click', function () {
                    pendingForm = null;
                    confirmDialog.close();
                });
            }

            var okBtn = confirmDialog.querySelector('[data-confirm-ok]');
            if (okBtn) {
                okBtn.addEventListener('click', function () {
                    var form = pendingForm;
                    pendingForm = null;
                    confirmDialog.close();
                    if (form) {
                        // The typed reason travels in the form's own hidden field, so the contract
                        // controller receives it exactly like any other posted value.
                        if (form.hasAttribute('data-confirm-reason') && reasonBox) {
                            var reasonField = form.querySelector('[data-confirm-value]');
                            if (reasonField) { reasonField.value = reasonBox.value; }
                        }
                        // form.submit() posts without running this listener again.
                        form.submit();
                    }
                });
            }
        }

        // The add / edit customer form: a business has no gender and no date of birth, so those
        // fields are hidden and emptied here. The service clears them again on the server, because
        // CHK_Customer_Business is the rule that really counts.
        var customerForm = document.querySelector('[data-customer-form]');
        if (customerForm) {
            var individualBlock = customerForm.querySelector('[data-individual-only]');
            var individualInputs = individualBlock ? individualBlock.querySelectorAll('input, select') : [];

            var syncCustomerType = function () {
                var selected = customerForm.querySelector('input[name="CustomerType"]:checked');
                var isBusiness = !!selected && selected.value === 'Business';

                if (individualBlock) { individualBlock.classList.toggle('hidden', isBusiness); }

                if (isBusiness) {
                    individualInputs.forEach(function (input) { input.value = ''; });
                }
            };

            customerForm.querySelectorAll('input[name="CustomerType"]').forEach(function (radio) {
                radio.addEventListener('change', syncCustomerType);
            });

            syncCustomerType();
        }

        // Contact dialog (M04): the list and the customer detail open the same form. The address
        // parameter ?create=true asks for it to be open on load, with the customer pre-selected.
        var contactDialog = document.querySelector('[data-contact-dialog]');
        if (contactDialog) {
            document.querySelectorAll('[data-open-contact]').forEach(function (button) {
                button.addEventListener('click', function () { contactDialog.showModal(); });
            });

            document.querySelectorAll('[data-close-contact]').forEach(function (button) {
                button.addEventListener('click', function () { contactDialog.close(); });
            });

            if (contactDialog.getAttribute('data-open') === 'true') {
                contactDialog.showModal();
            }
        }

        // Care form (M04): the satisfaction score belongs to a Survey. Hiding it is a convenience
        // only; the service clears a score on any other type and the database repeats the rule.
        var careForm = document.querySelector('[data-care-form]');
        if (careForm) {
            var careType = careForm.querySelector('#CareType');
            var scoreBlock = careForm.querySelector('[data-score-block]');

            var syncCareType = function () {
                if (!careType || !scoreBlock) { return; }

                var isSurvey = careType.value === 'Survey';
                scoreBlock.classList.toggle('hidden', !isSurvey);

                if (!isSurvey) {
                    scoreBlock.querySelectorAll('input[type="radio"]').forEach(function (radio) {
                        radio.checked = false;
                    });
                }
            };

            if (careType) { careType.addEventListener('change', syncCareType); }
            syncCareType();
        }

        // Feedback list (M04): one shared assign dialog; the button carries the feedback id and the
        // form action is pointed at that row just before the dialog opens.
        var assignDialog = document.getElementById('assignDialog');
        if (assignDialog) {
            var assignForm = document.getElementById('assignForm');

            document.querySelectorAll('[data-open-assign]').forEach(function (button) {
                button.addEventListener('click', function () {
                    if (assignForm) {
                        assignForm.action = '/feedbacks/' + button.getAttribute('data-open-assign') + '/assign';
                    }
                    assignDialog.showModal();
                });
            });

            document.querySelectorAll('[data-close-assign]').forEach(function (button) {
                button.addEventListener('click', function () { assignDialog.close(); });
            });
        }
    });
        // Master-detail contract form: add/remove lines, recompute totals, serialise to JSON on submit.
        var contractForm = document.querySelector('[data-contract-form]');
        if (contractForm) {
            var productSelect = document.getElementById('productOptions');
            var tbody = document.getElementById('linesBody');
            var totalCell = document.getElementById('totalValue');
            var jsonField = document.getElementById('linesJson');
            var errorBox = document.getElementById('linesError');

            var products = Array.from(productSelect ? productSelect.options : []).map(function (opt) {
                return { id: opt.value, name: opt.textContent, type: opt.getAttribute('data-type'), price: parseFloat(opt.getAttribute('data-price')) || 0 };
            });

            var formatMoney = function (value) {
                return new Intl.NumberFormat('vi-VN').format(value) + ' VNĐ';
            };

            var usedProductIds = function () {
                return Array.from(tbody.querySelectorAll('select.line-product')).map(function (s) { return s.value; });
            };

            var addLine = function (productId, quantity, unitPrice) {
                var row = document.createElement('tr');
                var productOptions = products.map(function (p) {
                    var disabled = (usedProductIds().indexOf(p.id) !== -1 && p.id !== (productId || '')) ? ' disabled' : '';
                    return '<option value="' + p.id + '"' + (p.id === (productId || '') ? ' selected' : '') + disabled + '>' + p.name + '</option>';
                }).join('');

                row.innerHTML =
                    '<td><select class="input line-product">' + productOptions + '</select></td>' +
                    '<td class="num"><input type="number" class="input line-qty" min="1" value="' + (quantity || 1) + '" /></td>' +
                    '<td class="num"><input type="number" class="input line-price" min="0" step="0.01" value="' + (unitPrice != null ? unitPrice : '') + '" /></td>' +
                    '<td class="num line-total">0</td>' +
                    '<td><button type="button" class="btn !py-1 !px-2.5" data-remove-line><i class="fa-solid fa-trash"></i></button></td>';

                tbody.appendChild(row);

                var prodSel = row.querySelector('.line-product');
                var qtyInput = row.querySelector('.line-qty');
                var priceInput = row.querySelector('.line-price');

                prodSel.addEventListener('change', function () {
                    var prod = products.find(function (p) { return p.id === prodSel.value; });
                    if (prod && (priceInput.value === '' || priceInput.value === '0')) {
                        priceInput.value = prod.price;
                    }
                    recompute();
                });

                qtyInput.addEventListener('input', recompute);
                priceInput.addEventListener('input', recompute);

                row.querySelector('[data-remove-line]').addEventListener('click', function () {
                    row.remove();
                    recompute();
                });

                if (priceInput.value === '') {
                    var prod = products.find(function (p) { return p.id === prodSel.value; });
                    if (prod) priceInput.value = prod.price;
                }
                recompute();
            };

            var recompute = function () {
                var total = 0;
                Array.from(tbody.querySelectorAll('tr')).forEach(function (row) {
                    var q = parseFloat(row.querySelector('.line-qty').value) || 0;
                    var p = parseFloat(row.querySelector('.line-price').value) || 0;
                    var lineTotal = q * p;
                    row.querySelector('.line-total').textContent = formatMoney(lineTotal);
                    total += lineTotal;
                });
                if (totalCell) totalCell.textContent = formatMoney(total);
                if (errorBox) errorBox.classList.add('hidden');
            };

            var addBtn = contractForm.querySelector('[data-add-line]');
            if (addBtn) {
                addBtn.addEventListener('click', function () {
                    var used = usedProductIds();
                    var next = products.find(function (p) { return used.indexOf(p.id) === -1; });
                    if (!next) {
                        if (errorBox) { errorBox.textContent = ''; errorBox.classList.remove('hidden'); }
                        return;
                    }
                    addLine(next.id, 1, next.price);
                });
            }

            contractForm.addEventListener('submit', function () {
                var lines = Array.from(tbody.querySelectorAll('tr')).map(function (row) {
                    return {
                        ProductId: parseInt(row.querySelector('.line-product').value, 10),
                        Quantity: parseInt(row.querySelector('.line-qty').value, 10) || 1,
                        UnitPrice: parseFloat(row.querySelector('.line-price').value) || 0
                    };
                });
                if (jsonField) jsonField.value = JSON.stringify(lines);
            });

            if (products.length > 0) {
                addLine(products[0].id, 1, products[0].price);
            }
        }

})();
