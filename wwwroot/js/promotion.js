/* เลื่อนชั้น: เลือกปี -> กำหนดรายคน -> สรุป -> ยืนยัน */
$(function () {
    'use strict';
    const ACTIONS = [
        { v: 'Promote', t: 'เลื่อนชั้น' }, { v: 'Retain', t: 'ซ้ำชั้น' }, { v: 'TransferDepartment', t: 'ย้ายแผนก' },
        { v: 'Suspend', t: 'พักการเรียน' }, { v: 'TransferOut', t: 'ย้ายสถานศึกษา' }, { v: 'Graduate', t: 'สำเร็จการศึกษา' }
    ];
    const NEEDS_ROOM = ['Promote', 'Retain', 'TransferDepartment'];
    let state = { students: [], rooms: [], request: null };

    function step(n) {
        [1, 2, 3].forEach(function (i) { $('#promotionStep' + i).toggleClass('d-none', i !== n); });
        $('#promotionSteps li').each(function () { $(this).toggleClass('active', Number($(this).data('step')) <= n); });
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function loadSourceRooms() {
        const $r = $('#pRoom').empty().append('<option value="">ทุกห้อง</option>');
        const year = $('#pFromYear').val();
        if (!year) return;
        App.getJson('StudentPromotion/GetClassrooms', { yearId: year }).done(function (list) {
            list.forEach(function (c) { $('<option>').val(c.id).text(c.name).appendTo($r); });
        });
    }
    $('#pFromYear').on('change', loadSourceRooms);
    loadSourceRooms();

    function collectRules() {
        return $('.promotion-rule').map(function () {
            const v = this.value;
            return {
                gradeLevelId: Number($(this).data('grade')),
                graduate: v === 'graduate',
                nextGradeLevelId: v === 'graduate' || v === 'retain' ? null : Number(v)
            };
        }).get();
    }

    function roomsFor(s, action) {
        return state.rooms.filter(function (c) {
            if (action === 'Promote') return c.departmentId === s.departmentId && c.gradeLevelId !== s.gradeLevelId;
            if (action === 'Retain') return c.departmentId === s.departmentId && c.gradeLevelId === s.gradeLevelId;
            if (action === 'TransferDepartment') return c.departmentId !== s.departmentId;
            return false;
        });
    }

    function fillRoomSelect($select, s, action, preferred) {
        $select.empty();
        if (NEEDS_ROOM.indexOf(action) < 0) {
            $select.prop('disabled', true).append('<option value="">ไม่ต้องเลือกห้อง</option>');
            return;
        }
        $select.prop('disabled', false).append('<option value="">เลือกห้องปลายทาง</option>');
        roomsFor(s, action).forEach(function (c) {
            $('<option>').val(c.id).text(action === 'TransferDepartment' ? c.name + ' · ' + c.departmentName : c.name).appendTo($select);
        });
        if (preferred) $select.val(String(preferred));
    }

    function renderRows() {
        const $body = $('#pRows').empty();
        state.students.forEach(function (s) {
            const $tr = $('<tr>').attr('data-id', s.studentProfileId);
            $('<td>').append($('<input class="form-check-input p-check" type="checkbox" checked>').attr('aria-label', 'เลือก ' + s.fullName)).appendTo($tr);
            $('<td>').append($('<div class="fw-semibold text-nowrap">').text(s.fullName)).append($('<div class="small text-muted-soft">').text(s.studentCode)).appendTo($tr);
            $('<td>').append($('<div class="text-nowrap">').text(s.classroomName)).append($('<div class="small text-muted-soft">').text(s.departmentName + ' · ' + s.statusName)).appendTo($tr);

            const $action = $('<select class="form-select form-select-sm p-action">').attr('aria-label', 'การดำเนินการของ ' + s.fullName);
            ACTIONS.forEach(function (a) { $('<option>').val(a.v).text(a.t).appendTo($action); });
            $action.val(s.suggestedAction);
            $('<td>').append($action).appendTo($tr);

            const $room = $('<select class="form-select form-select-sm p-room">').attr('aria-label', 'ห้องปลายทางของ ' + s.fullName);
            fillRoomSelect($room, s, s.suggestedAction, s.suggestedClassroomId);
            $('<td>').append($room).appendTo($tr);

            $('<td>').append($('<input type="text" class="form-control form-control-sm p-note" maxlength="500" placeholder="ถ้ามี">').attr('aria-label', 'หมายเหตุของ ' + s.fullName)).appendTo($tr);
            $body.append($tr);
        });
        $('#pCount').text('นักเรียน ' + state.students.length + ' คน');
        $('#pCheckAll').prop('checked', true);
    }

    function studentOf($tr) {
        const id = Number($tr.attr('data-id'));
        return state.students.find(function (s) { return s.studentProfileId === id; });
    }

    $('#pRows').on('change', '.p-action', function () {
        const $tr = $(this).closest('tr');
        fillRoomSelect($tr.find('.p-room'), studentOf($tr), this.value, null);
    });
    $('#pRows').on('change', '.p-check', function () { $(this).closest('tr').toggleClass('row-off', !this.checked); });
    $('#pCheckAll').on('change', function () {
        const on = this.checked;
        $('#pRows .p-check').prop('checked', on).trigger('change');
    });

    const $bulk = $('#pBulkAction');
    ACTIONS.forEach(function (a) { $('<option>').val(a.v).text(a.t).appendTo($bulk); });
    $('#pBulkApply').on('click', function () {
        const action = $bulk.val();
        $('#pRows tr').each(function () {
            const $tr = $(this);
            if (!$tr.find('.p-check').prop('checked')) return;
            $tr.find('.p-action').val(action).trigger('change');
        });
    });

    $('#pCloneRooms').on('click', function () {
        const from = $('#pFromYear').val(), to = $('#pToYear').val();
        if (!from || !to) { App.toast('กรุณาเลือกปีต้นทางและปลายทาง', 'error'); return; }
        App.confirm({
            title: 'สร้างห้องเรียนปีปลายทาง',
            message: 'คัดลอกโครงห้องเรียนจากปีต้นทางไปปีปลายทาง โดยข้ามห้องที่มีอยู่แล้ว ใช่หรือไม่',
            button: 'สร้างห้องเรียน',
            onConfirm: function () {
                return App.post(App.url('StudentPromotion/CloneClassrooms?fromYearId=' + from + '&toYearId=' + to))
                    .done(function (r) { App.toast(r.message); })
                    .fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
            }
        });
    });

    $('#pLoad').on('click', function () {
        const num = function (sel) { const v = $(sel).val(); return v ? Number(v) : null; };
        state.request = {
            fromYearId: num('#pFromYear'), toYearId: num('#pToYear'),
            departmentId: num('#pDept'), gradeLevelId: num('#pGrade'), classroomId: num('#pRoom'),
            rules: collectRules()
        };
        if (!state.request.toYearId) { App.toast('กรุณาเลือกปีปลายทาง', 'error'); return; }
        App.postJson('StudentPromotion/GetStudents', state.request).done(function (r) {
            state.students = r.data.students;
            state.rooms = r.data.targetClassrooms;
            if (!state.students.length) { App.toast('ไม่พบนักเรียนในปีต้นทางตามเงื่อนไขนี้', 'error'); return; }
            renderRows();
            step(2);
        }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
    });
    $('#pBack1').on('click', function () { step(1); });
    $('#pBack2').on('click', function () { step(2); });

    function collectItems() {
        return $('#pRows tr').filter(function () { return $(this).find('.p-check').prop('checked'); }).map(function () {
            const $tr = $(this), room = $tr.find('.p-room').val();
            return {
                studentProfileId: Number($tr.attr('data-id')),
                action: $tr.find('.p-action').val(),
                toClassroomId: room ? Number(room) : null,
                note: $tr.find('.p-note').val() || null
            };
        }).get();
    }

    function payload() {
        return { fromYearId: state.request.fromYearId, toYearId: state.request.toYearId, note: $('#pNote').val() || null, items: collectItems() };
    }

    function renderPreview(p) {
        $('#pPreviewTitle').text('สรุป ' + p.fromYear + ' → ' + p.toYear + ' รวม ' + p.total + ' คน');
        const $s = $('#pSummary').empty();
        p.summary.forEach(function (x) {
            const $card = $('<div class="surface stat-card">');
            $('<div class="stat-number">').text(x.value).appendTo($card);
            $('<p class="stat-label">').text(x.key).appendTo($card);
            $s.append($card);
        });
        const $r = $('#pPreviewRows').empty();
        p.rows.forEach(function (x) {
            const $tr = $('<tr>');
            $('<td>').append($('<div class="fw-semibold text-nowrap">').text(x.fullName)).append($('<div class="small text-muted-soft">').text(x.studentCode)).appendTo($tr);
            $('<td class="text-nowrap">').text(x.actionText).appendTo($tr);
            $('<td>').text(x.fromText).appendTo($tr);
            $('<td>').text(x.toText).appendTo($tr);
            const $check = $('<td>').appendTo($tr);
            if (x.error) $('<span class="invalid-text">').text(x.error).appendTo($check);
            else $('<span class="status-pill status-published">').text('พร้อมดำเนินการ').appendTo($check);
            $r.append($tr);
        });
        $('#pPreviewError').toggleClass('d-none', p.errorCount === 0).text(p.errorCount ? 'มีรายการไม่ถูกต้อง ' + p.errorCount + ' รายการ กรุณากลับไปแก้ไขก่อนยืนยัน' : '');
        $('#pConfirm').prop('disabled', p.errorCount > 0);
    }

    $('#pPreview').on('click', function () {
        const data = payload();
        if (!data.items.length) { App.toast('กรุณาเลือกนักเรียนอย่างน้อย 1 คน', 'error'); return; }
        App.postJson('StudentPromotion/Preview', data).done(function (r) {
            renderPreview(r.data);
            step(3);
        }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
    });

    $('#pConfirm').on('click', function () {
        const data = payload();
        App.confirm({
            title: 'ยืนยันการเลื่อนชั้น',
            message: 'ดำเนินการกับนักเรียน ' + data.items.length + ' คน ระบบจะอัปเดตข้อมูลปัจจุบันและบันทึกประวัติ ใช่หรือไม่',
            button: 'ยืนยันดำเนินการ',
            onConfirm: function () {
                return App.postJson('StudentPromotion/Confirm', data).done(function (r) {
                    App.toast(r.message);
                    window.setTimeout(function () { location.href = App.url('StudentPromotion/HistoryDetails/' + r.data); }, 700);
                }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
            }
        });
    });
});
