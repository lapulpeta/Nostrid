export function setTheme(theme) {
    var attribute = "data-bs-theme";
    if (theme!==null) {
        document.body.setAttribute(attribute, theme);
    }
    return document.body.getAttribute(attribute);
}

export function hideOffcanvasMenu() {
    if (!bootstrap) return;
    var menu = document.getElementById("offcanvasMenu");
    if (!menu) return;
    var instance = bootstrap.Offcanvas.getInstance(menu);
    if (!instance) return;
    instance.hide();
}

export function hideModal(modalId) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getInstance(document.getElementById(modalId));
    if (!modal) return;
    modal.hide();
}