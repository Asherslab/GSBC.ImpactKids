// wwwroot/js/analyticsSortable.js
// Drag and drop for the Analytics menu's admin page - the same approach as scheduleSortable.js, and
// SortableJS for the same reason: MudBlazor's drop container is built on HTML5 drag events, which
// iPhone Safari does not reliably deliver from a touch.
//
// Two levels. The top list (data-parent-id="") holds dashboards, questions and folders; each folder
// holds a list of its own (data-parent-id="<folder id>"). Dashboards and questions move freely between
// all of them; a folder moves only within the top list.
//
// Blazor owns the DOM, so a drop is reported and then undone here: the element goes back where it
// came from, and the component re-renders the new order itself.

import Sortable from '../lib/sortablejs/sortable.esm.js';

/**
 * Makes every not-yet-attached list under `root` sortable. Safe to call after every render: lists
 * already attached are skipped, and lists Blazor has removed simply go away with their element.
 */
export function attach(root, dotnet) {
    if (!root) return;

    for (const list of root.querySelectorAll('[data-parent-id]')) {
        if (list._sortable) continue;

        list._sortable = Sortable.create(list, {
            group: {
                name: 'analytics',
                put: (to, _from, dragged) => dragged.dataset.folder !== 'true' || to.el.dataset.parentId === ''
            },
            handle: '.analytics-drag-handle',
            draggable: '[data-embed-id]',
            animation: 150,
            fallbackOnBody: true,
            swapThreshold: 0.65,
            ghostClass: 'analytics-drag-ghost',
            onEnd: (evt) => {
                const moved = evt.from !== evt.to || evt.oldDraggableIndex !== evt.newDraggableIndex;
                if (!moved) return;

                // Undo SortableJS's DOM move; Blazor renders the result.
                const from = evt.from;
                const sameList = evt.from === evt.to;
                const before = sameList && evt.oldIndex > evt.newIndex
                    ? from.children[evt.oldIndex + 1] ?? null
                    : from.children[evt.oldIndex] ?? null;
                from.insertBefore(evt.item, before);

                dotnet.invokeMethodAsync(
                    'OnEmbedDropped',
                    evt.item.dataset.embedId,
                    evt.to.dataset.parentId || null,
                    evt.newDraggableIndex
                );
            }
        });
    }
}
