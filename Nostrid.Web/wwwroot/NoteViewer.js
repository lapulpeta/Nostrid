export function isVisible(element) {
    var position = element.getBoundingClientRect();
    return position.bottom > 0 && window.innerHeight > position.top;
}