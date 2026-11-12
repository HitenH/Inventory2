// Keyboard shortcuts. The Alt + letter list and what each does lives in Service/Shortcuts.cs; this file only
// catches the keys and hands them to the app. Other Alt keys (e.g. Alt+F for Chrome's menu) are left alone.
// Ctrl+Alt (AltGr on some keyboards) is ignored so typing special characters still works.
window.inventoryShortcuts = {
    init: function (dotNetRef, letters) {
        if (window.inventoryShortcuts._handler) {
            document.removeEventListener('keydown', window.inventoryShortcuts._handler, true);
        }
        const wanted = new Set(letters);
        const handler = function (e) {
            if (e.repeat || e.metaKey) return;

            // Enter in a data-entry area (marked "enter-moves-on": the line forms, Purchase Orders), owner's choice:
            // in a typing box (Quantity, Rate, Discount, Remarks ...) it saves the line at once, as it always did;
            // on a drop-down (Variant) it moves on to the next box. Search boxes have their own Enter.
            if (e.key === 'Enter' && !e.ctrlKey && !e.altKey && !e.shiftKey) {
                const t = e.target;
                const area = t && t.closest ? t.closest('.enter-moves-on') : null;
                if (area && !t.closest('.lookup-box') && (t.tagName === 'SELECT'
                    || (t.tagName === 'INPUT' && !/^(button|submit|checkbox|radio|file)$/i.test(t.type)))) {
                    e.preventDefault();
                    e.stopPropagation();
                    if (t.tagName === 'SELECT') {
                        window.inventoryShortcuts.next(area, t);
                    } else {
                        window.inventoryShortcuts.submitArea(area, t);
                    }
                    return;
                }
            }

            // Ctrl+S: press the screen's main Save button (marked data-shortcut="save").
            if (e.ctrlKey && !e.altKey && !e.shiftKey && e.code === 'KeyS') {
                e.preventDefault();   // instead of Chrome's "Save page as"
                e.stopPropagation();
                window.inventoryShortcuts.save();
                return;
            }

            if (!e.altKey || e.ctrlKey) return;
            if (!e.code || !e.code.startsWith('Key')) return;
            const letter = e.code.substring(3);
            if (!wanted.has(letter)) return;
            e.preventDefault();
            e.stopPropagation();
            dotNetRef.invokeMethodAsync('OnShortcut', letter, e.shiftKey);
        };
        window.inventoryShortcuts._handler = handler;
        document.addEventListener('keydown', handler, true);

        // Esc: back to the list, and from a list to the Dashboard. Listens after everything else, so when Esc
        // is closing something - a pop-up window, a search list, a calendar, the shortcut list - it only does that.
        if (window.inventoryShortcuts._escHandler) {
            document.removeEventListener('keydown', window.inventoryShortcuts._escHandler, false);
        }
        const escHandler = function (e) {
            // Enter in a customer/supplier/product search box only picks the name (the app then moves on);
            // it must not also press the screen's Save button, which used to give "Please select a supplier".
            if (e.key === 'Enter' && e.target && e.target.closest && e.target.closest('.lookup-box')) {
                e.preventDefault();
                return;
            }
            // Up/Down in a search pop-up's search bar move the highlighted row; stop them also jumping the text cursor.
            if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && e.target && e.target.classList
                && e.target.classList.contains('lookup-dialog-search')) {
                e.preventDefault();
                return;
            }
            if (e.key !== 'Escape' || e.repeat || e.defaultPrevented) return;
            if (e.ctrlKey || e.altKey || e.shiftKey || e.metaKey) return;
            if (document.querySelector('.mud-dialog-container, .mud-overlay, .mud-popover-open:not(.mud-tooltip)')) return;
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnBack');
        };
        window.inventoryShortcuts._escHandler = escHandler;
        document.addEventListener('keydown', escHandler, false);
    },

    dispose: function () {
        if (window.inventoryShortcuts._handler) {
            document.removeEventListener('keydown', window.inventoryShortcuts._handler, true);
            window.inventoryShortcuts._handler = null;
        }
        if (window.inventoryShortcuts._escHandler) {
            document.removeEventListener('keydown', window.inventoryShortcuts._escHandler, false);
            window.inventoryShortcuts._escHandler = null;
        }
    },

    // Clicks the first visible, enabled Save button. The box being typed in is left first, so its value is
    // taken (the app only takes a typed value when the box is left) - exactly like clicking Save with the mouse.
    save: function () {
        const buttons = Array.from(document.querySelectorAll('[data-shortcut="save"]'));
        const button = buttons.find(b => !b.disabled && b.offsetParent !== null);
        if (!button) return;
        if (document.activeElement && document.activeElement !== document.body) {
            document.activeElement.blur();
        }
        button.click();
    },

    // Enter moves on: the next box of the area that can be typed in (skipping greyed-out and hidden ones);
    // after the last one, the area's add button is pressed. Leaving the box first makes the app take its value.
    next: function (area, from) {
        const fields = Array.from(area.querySelectorAll('input, select, textarea')).filter(function (f) {
            return f.type !== 'hidden' && !f.disabled && !f.readOnly && f.offsetParent !== null
                && !/^(button|submit|checkbox|radio|file)$/i.test(f.type);
        });
        const i = fields.indexOf(from);
        const target = i >= 0 ? fields[i + 1] : null;
        if (target) {
            target.focus();
            if (target.select && target.tagName === 'INPUT') target.select();
            return;
        }
        window.inventoryShortcuts.submitArea(area, from);
    },

    // Presses the area's add button (data-enter-submit). Leaving the box first makes the app take its value.
    submitArea: function (area, from) {
        const submit = area.querySelector('[data-enter-submit]');
        if (submit) {
            from.blur();
            setTimeout(function () { submit.click(); }, 0);
        }
    },

    // Keeps the highlighted row of a long list in view while Up/Down move through it.
    scrollIntoView: function (selector) {
        const el = document.querySelector(selector);
        if (el) el.scrollIntoView({ block: 'nearest' });
    },

    // Puts the cursor in the box matching the selector. The screen may still be loading, so it retries
    // for about a second. A selector on a MudBlazor field finds the text box inside it.
    // The screen sometimes redraws the box as a new one right after (e.g. after a line is added), which drops
    // the cursor. So for about a second afterwards: if the cursor has been dropped (it is nowhere), put it back.
    // If the operator has clicked or tabbed somewhere else, that is left alone.
    focus: function (selector) {
        // Only the latest request counts: an older one still waiting or watching stops, so two parts of the
        // screen can never pull the cursor back and forth between them.
        const ticket = (window.inventoryShortcuts._focusTicket = (window.inventoryShortcuts._focusTicket || 0) + 1);
        const current = function () { return ticket === window.inventoryShortcuts._focusTicket; };
        const find = function () {
            let el = document.querySelector(selector);
            if (el && !/^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName)) {
                el = el.querySelector('input, textarea, select');
            }
            return el && !el.disabled ? el : null;
        };
        // "Dropped" = nowhere, gone from the page, or parked on an invisible holder (MudBlazor's pop-up cursor trap
        // parks it on a tabindex=-1 div right after opening). A real box or button the operator chose is left alone.
        const dropped = function () {
            const a = document.activeElement;
            if (!a || a === document.body || !a.isConnected) return true;
            return a.getAttribute('tabindex') === '-1' && !/^(INPUT|TEXTAREA|SELECT|BUTTON|A)$/.test(a.tagName);
        };
        let tries = 0;
        const attempt = function () {
            if (!current()) return;
            const el = find();
            if (el) {
                el.focus();
                let watch = 0;
                const keep = function () {
                    if (++watch > 20 || !current()) return;
                    if (dropped()) {
                        const again = find();
                        if (again) again.focus();
                    }
                    setTimeout(keep, 50);
                };
                setTimeout(keep, 50);
                return;
            }
            if (++tries < 20) setTimeout(attempt, 50);
        };
        attempt();
    }
};
