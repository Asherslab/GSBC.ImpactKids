// wwwroot/js/scheduleSortable.js
// Drag and drop for a schedule's items, on phones as well as desktops.
//
// SortableJS rather than MudBlazor's drop container: that one is built on HTML5 drag events, which
// iPhone Safari does not reliably deliver from a touch, and these lists are dragged mid-programme on
// phones. SortableJS has a touch path of its own and drags between lists (lane to lane).
//
// Blazor owns the DOM, so a drop is reported and then undone here: the element goes back where it
// came from, and the component re-renders the new order itself. Leaving SortableJS's move in place
// would put the DOM out of step with Blazor's render tree, and the next render would scramble it.

import Sortable from '../lib/sortablejs/sortable.esm.js';

/**
 * Makes every not-yet-attached lane list under `root` sortable. Safe to call after every render:
 * lists already attached are skipped, and lists Blazor has removed simply go away with their element.
 */
export function attach(root, dotnet) {
    if (!root) return;

    for (const list of root.querySelectorAll('[data-lane-id]')) {
        if (list._sortable) continue;

        list._sortable = Sortable.create(list, {
            group: root.dataset.scheduleId || 'schedule',
            handle: '.schedule-drag-handle',
            draggable: '[data-item-id]',
            animation: 150,
            // A touch must be held briefly before it drags, so a phone can still scroll the page by
            // swiping over the list. Mouse drags start at once.
            delay: 150,
            delayOnTouchOnly: true,
            touchStartThreshold: 4,
            ghostClass: 'schedule-drag-ghost',
            onEnd: (evt) => {
                const itemId = evt.item.dataset.itemId;
                const laneId = evt.to.dataset.laneId;
                const position = evt.newDraggableIndex;
                const moved = evt.from !== evt.to || evt.oldDraggableIndex !== evt.newDraggableIndex;

                // Undo SortableJS's DOM move; Blazor renders the result.
                const from = evt.from;
                const before = from.children[evt.oldIndex] ?? null;
                if (evt.from !== evt.to || evt.oldIndex !== evt.newIndex) {
                    from.insertBefore(evt.item, evt.oldIndex > evt.newIndex && evt.from === evt.to
                        ? from.children[evt.oldIndex + 1] ?? null
                        : before);
                }

                if (moved)
                    dotnet.invokeMethodAsync('OnItemDropped', itemId, laneId, position);
            }
        });
    }
}
