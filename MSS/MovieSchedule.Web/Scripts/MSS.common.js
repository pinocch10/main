function pad(num, size) {
    var s = num + "";
    while (s.length < size) s = "0" + s;
    return s;
}

function setSelectedIndex(s, valsearch) {
    // Loop through all the items in drop down list
    for (i = 0; i < s.options.length; i++) {
        if (s.options[i].value == valsearch) {
            // Item is found. Set its property and exit
            s.options[i].selected = true;
            break;
        }
    }
    return;
}

function replaceAll(find, replace, str) {
    return str.replace(new RegExp(find, 'g'), replace);
}