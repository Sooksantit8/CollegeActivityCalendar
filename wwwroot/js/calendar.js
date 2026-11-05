/* ปฏิทิน FullCalendar โหลดข้อมูลผ่าน jQuery AJAX */
$(function () {
    'use strict';
    const el = document.getElementById('calendar');
    if (!el) return;
    const narrow = window.matchMedia('(max-width: 767.98px)').matches;
    const canCreate = $(el).data('can-create') === true;

    function filters() {
        const data = {};
        $('#eventFilterForm').serializeArray().forEach(function (x) { if (x.value !== '') data[x.name] = x.value; });
        return data;
    }

    const calendar = new FullCalendar.Calendar(el, {
        locale: 'th',
        initialView: narrow ? 'listMonth' : ($(el).data('default-view') || 'dayGridMonth'),
        height: 'auto',
        dayMaxEvents: 3,
        nowIndicator: true,
        eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
        slotLabelFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
        slotMinTime: '07:00:00',
        slotMaxTime: '20:00:00',
        headerToolbar: { left: 'prev,next today', center: 'title', right: 'dayGridMonth,timeGridWeek,listMonth' },
        buttonText: { today: 'วันนี้', dayGridMonth: 'เดือน', timeGridWeek: 'สัปดาห์', listMonth: 'รายการ' },
        noEventsContent: 'ไม่มีกิจกรรมในช่วงนี้',
        events: function (info, success, failure) {
            // ส่งเฉพาะวันที่ ไม่ให้เขตเวลาของเบราว์เซอร์ทำให้ช่วงวันเลื่อน
            const query = $.extend({ start: info.startStr.slice(0, 10), end: info.endStr.slice(0, 10) }, filters());
            App.getJson('Calendar/GetEvents', query)
                .done(function (list) { success(list); })
                .fail(function (xhr) {
                    App.toast(App.errorMessage(xhr), 'error');
                    failure(new Error('load failed'));
                });
        },
        eventDidMount: function (arg) {
            const p = arg.event.extendedProps;
            arg.el.setAttribute('title', arg.event.title + ' · ' + p.levelText + ' · ' + p.statusText + (p.locationText ? ' · ' + p.locationText : ''));
        },
        eventClick: function (arg) {
            arg.jsEvent.preventDefault();
            EventDetails.show(arg.event.id);
        },
        dateClick: function (arg) {
            if (canCreate) EventForm.add(arg.dateStr.slice(0, 10));
        }
    });
    calendar.render();

    const refresh = function () { calendar.refetchEvents(); };
    $('#eventFilterForm').on('change', 'select', refresh)
        .on('input', 'input[type=search]', App.debounce(refresh, 300))
        .on('submit', function (e) { e.preventDefault(); refresh(); })
        .on('reset', function () { window.setTimeout(refresh, 0); });
    $(document).on('event:changed', refresh);
});
