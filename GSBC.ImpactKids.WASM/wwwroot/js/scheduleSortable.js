// wwwroot/js/scheduleSortable.js
// Drag and drop for a schedule, on phones as well as desktops.
//
// SortableJS rather than MudBlazor's drop container: that one is built on HTML5 drag events, which
// iPhone Safari does not reliably deliver from a touch, and these lists are dragged mid-programme on
// phones. SortableJS has a touch path of its own and drags between lists.
//
// The lists nest. The main list (data-lane-id="main") holds items, sections and whole splits; each
// split holds its lanes' lists; each section holds its items' list (data-section-id). Items move
// freely between all of them. A split moves only within the main list, and a section never goes
// under another section.
//
// Blazor owns the DOM, so a drop is reported and then undone here: the element goes back where it
// came from, and the component re-renders the new order itself. Leaving SortableJS's move in place
// would put the DOM out of step with Blazor's render tree, and the next render would scramble it.

import Sortable from '../lib/sortablejs/sortable.esm.js';

/**
 * Makes every not-yet-attached list under `root` sortable. Safe to call after every render: lists
 * already attached are skipped, and lists Blazor has removed simply go away with their element.
 */
export function attach(root, dotnet) {
    if (!root) return;

    const name = root.dataset.scheduleId || 'schedule';

    for (const list of root.querySelectorAll('[data-lane-id], [data-section-id]')) {
        if (list._sortable) continue;

        list._sortable = Sortable.create(list, {
            group: {
                name,
                put: (to, _from, dragged) => {
                    if (dragged.dataset.blockId) return to.el.dataset.laneId === 'main';
                    if (dragged.dataset.section === 'true') return !to.el.dataset.sectionId;
                    return true;
                }
            },
            handle: '.schedule-drag-handle',
            draggable: '[data-item-id], [data-block-id]',
            animation: 150,
            // Nested lists: settle on the inner list only once the pointer is well inside it.
            fallbackOnBody: true,
            swapThreshold: 0.65,
            // A touch must be held briefly before it drags, so a phone can still scroll the page by
            // swiping over the list. Mouse drags start at once.
            delay: 150,
            delayOnTouchOnly: true,
            touchStartThreshold: 4,
            ghostClass: 'schedule-drag-ghost',
            onEnd: (evt) => {
                const moved = evt.from !== evt.to || evt.oldDraggableIndex !== evt.newDraggableIndex;
                const position = evt.newDraggableIndex;
                const laneId = evt.to.dataset.laneId ?? null;
                const sectionId = evt.to.dataset.sectionId ?? null;
                const { itemId, blockId } = evt.item.dataset;

                // Undo SortableJS's DOM move; Blazor renders the result.
                if (moved) {
                    const from = evt.from;
                    const sameList = evt.from === evt.to;
                    const before = sameList && evt.oldIndex > evt.newIndex
                        ? from.children[evt.oldIndex + 1] ?? null
                        : from.children[evt.oldIndex] ?? null;
                    from.insertBefore(evt.item, before);
                }

                if (!moved) return;

                if (blockId)
                    dotnet.invokeMethodAsync('OnSplitDropped', blockId, position);
                else
                    dotnet.invokeMethodAsync('OnItemDropped', itemId, laneId, sectionId, position);
            }
        });
    }
}
