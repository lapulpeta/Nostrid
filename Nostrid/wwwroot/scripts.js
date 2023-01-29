export function setTheme(theme) {
    var attribute = "data-bs-theme";
    if (theme !== null) {
        document.body.setAttribute(attribute, theme);
    }
    return document.body.getAttribute(attribute);
}

export function getCssVariable(name) {
    return getComputedStyle(document.body)
        .getPropertyValue(name)
        .replace(RegExp("^#([0-9a-fA-F])([0-9a-fA-F])([0-9a-fA-F])$"), "#$1$1$2$2$3$3")
}

export function hideOffcanvasMenu() {
    if (!bootstrap) return;
    var menu = document.getElementById("offcanvasMenu");
    if (!menu) return;
    var instance = bootstrap.Offcanvas.getInstance(menu);
    if (!instance) return;
    instance.hide();
}

export function showModal(element) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(element.firstChild);
    if (!modal) return;
    modal.show();
}

export function hideModal(element) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(element.firstChild);
    if (!modal) return;
    modal.hide();
}

export function isModalShown(element) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(element.firstChild);
    if (!modal) return;
    return modal._isShown;
}