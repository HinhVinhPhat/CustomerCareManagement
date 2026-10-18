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
    });
})();
