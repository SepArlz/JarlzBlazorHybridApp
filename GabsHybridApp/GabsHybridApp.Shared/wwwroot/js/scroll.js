window.gabsScroll = {
    initHorizontalScroll: (elementId) => {
        const element = document.getElementById(elementId);
        if (!element) return;

        // 1. Wheel scroll mapping (vertical scroll to horizontal scroll)
        element.addEventListener('wheel', (e) => {
            if (e.deltaY !== 0) {
                e.preventDefault();
                element.scrollLeft += e.deltaY;
            }
        }, { passive: false });

        // 2. Mouse drag scrolling
        let isDown = false;
        let startX;
        let scrollLeft;
        let hasDragged = false;

        element.style.cursor = 'grab';

        element.addEventListener('mousedown', (e) => {
            isDown = true;
            hasDragged = false;
            element.style.cursor = 'grabbing';
            element.style.userSelect = 'none'; // prevent text selection
            startX = e.pageX - element.offsetLeft;
            scrollLeft = element.scrollLeft;
        });

        element.addEventListener('mouseleave', () => {
            if (isDown) {
                isDown = false;
                element.style.cursor = 'grab';
                element.style.removeProperty('user-select');
            }
        });

        element.addEventListener('mouseup', () => {
            if (isDown) {
                isDown = false;
                element.style.cursor = 'grab';
                element.style.removeProperty('user-select');
            }
        });

        element.addEventListener('mousemove', (e) => {
            if (!isDown) return;
            e.preventDefault();
            const x = e.pageX - element.offsetLeft;
            const walk = (x - startX) * 1.5; // multiplier for scroll speed
            if (Math.abs(walk) > 5) {
                hasDragged = true;
            }
            element.scrollLeft = scrollLeft - walk;
        });

        // 3. Prevent click on drag
        element.addEventListener('click', (e) => {
            if (hasDragged) {
                e.preventDefault();
                e.stopPropagation();
                hasDragged = false;
            }
        }, true); // Capture phase is key to stop children from receiving the click
    }
};

window.gabsTheme = {
    setDocumentTheme: function (isDark) {
        try {
            if (isDark) {
                document.documentElement.classList.add('dark-theme');
            } else {
                document.documentElement.classList.remove('dark-theme');
            }
        } catch (e) { }
    }
};

