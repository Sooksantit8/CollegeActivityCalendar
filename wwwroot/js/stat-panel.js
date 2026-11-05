/* การ์ดและแถบสรุป: คลิกแล้วแสดงรายการในหน้าเดิมโดยไม่เปลี่ยนหน้า ใช้ในหน้าภาพรวมและรายงาน */
$(function () {
    'use strict';
    const $cards = $('[data-stat-list]');
    const $panel = $('#statPanel');
    const $body = $('#statPanelBody');
    let $active = null;
    let requestId = 0;

    function load(page) {
        const id = ++requestId;
        const url = $active.attr('data-stat-list') + (page > 1 ? '&page=' + page : '');
        $body.attr('aria-busy', 'true');
        $.ajax({ url: url, type: 'GET', dataType: 'html' })
            .done(function (html) {
                // คลิกการ์ดอื่นระหว่างรอ ให้ใช้ผลของการ์ดล่าสุดเท่านั้น
                if (id === requestId) $body.html(html);
            })
            .fail(function (xhr) {
                if (id !== requestId) return;
                $body.empty().append($('<div class="alert alert-danger mb-0" role="alert">').text(App.errorMessage(xhr)));
            })
            .always(function () { if (id === requestId) $body.removeAttr('aria-busy'); });
    }

    function keyOf($card) {
        return $card.attr('data-stat-key') || String($cards.index($card));
    }

    function setHash(key) {
        // จำการ์ดที่เปิดไว้ เพื่อให้แสดงต่อหลังหน้ารีเฟรช เช่น หลังแก้ไขกิจกรรม
        history.replaceState(null, '', location.pathname + location.search + (key === null ? '' : '#card=' + key));
    }

    function close() {
        requestId++;
        if ($active) $active.removeClass('active').attr('aria-expanded', 'false');
        $active = null;
        $panel.addClass('d-none');
        $body.empty();
        setHash(null);
    }

    function open($card, scroll) {
        if ($active) $active.removeClass('active').attr('aria-expanded', 'false');
        $active = $card.addClass('active').attr('aria-expanded', 'true');
        $('#statPanelTitle').text($card.attr('data-stat-title'));
        $('#statPanelFull').attr('href', $card.attr('href'));
        $body.empty().append('<p class="text-muted-soft mb-0 py-3 text-center">กำลังโหลด...</p>');
        $panel.removeClass('d-none');
        setHash(keyOf($card));
        load(1);
        if (scroll) {
            const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            $panel[0].scrollIntoView({ block: 'nearest', behavior: reduce ? 'auto' : 'smooth' });
            $panel[0].focus({ preventScroll: true });
        }
    }

    $cards.on('click', function (e) {
        // Ctrl/Cmd + คลิก ยังเปิดแท็บใหม่ได้ตามปกติ
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
        e.preventDefault();
        const $card = $(this);
        if ($active && $active[0] === this) close();
        else open($card, true);
    });

    $('#statPanelClose').on('click', function () {
        const $card = $active;
        close();
        if ($card) $card.trigger('focus');
    });
    $body.on('click', '[data-page]', function () { load(Number($(this).data('page'))); });

    // เพิ่ม แก้ไข หรือยกเลิกกิจกรรมแล้ว ตัวเลขบนการ์ดเปลี่ยน จึงโหลดหน้าใหม่
    $(document).on('event:changed', function () { window.setTimeout(function () { location.reload(); }, 400); });

    const match = /^#card=([\w-]+)$/.exec(location.hash);
    if (match) {
        const $saved = $cards.filter(function () { return keyOf($(this)) === match[1]; }).first();
        if ($saved.length) open($saved, false);
    }
});
