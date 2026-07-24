mergeInto(LibraryManager.library, {
    IsMobileDevice: function () {
        return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)
            || (navigator.maxTouchPoints > 1 && /Macintosh/i.test(navigator.userAgent));
    }
});
