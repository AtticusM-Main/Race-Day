document.addEventListener('submit', function (event) {
    var form = event.target;
    var message = form.getAttribute('data-confirm');
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

document.querySelectorAll('input[type="file"][data-preview]').forEach(function (input) {
    input.addEventListener('change', function () {
        var target = document.querySelector(input.getAttribute('data-preview'));
        var file = input.files && input.files[0];
        if (!target || !file) { return; }
        if (!file.type.startsWith('image/')) { return; }
        target.src = URL.createObjectURL(file);
        target.hidden = false;
    });
});

document.querySelectorAll('form').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        var button = form.querySelector('button[type="submit"][data-busy]');
        if (button && form.checkValidity()) {
            setTimeout(function () {
                // jQuery unobtrusive validation turns off the browser's own checks, so ask it too.
                var jqueryValid = !(window.jQuery && jQuery.fn.valid) || jQuery(form).valid();
                if (event.defaultPrevented || !jqueryValid) return;
                button.disabled = true;
                button.textContent = button.getAttribute('data-busy');
            }, 0);
        }
    });
});
