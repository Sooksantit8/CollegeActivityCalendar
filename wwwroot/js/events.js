/* รายละเอียดกิจกรรม ฟอร์มเพิ่ม/แก้ไข และรายการกิจกรรม */
(function ($) {
    'use strict';

    function changed() { $(document).trigger('event:changed'); }

    function runAction(action, id, title) {
        const texts = {
            Publish: { t: 'เผยแพร่กิจกรรม', m: 'เผยแพร่ "' + title + '" ทันทีใช่หรือไม่', b: 'เผยแพร่' },
            Cancel: { t: 'ยกเลิกกิจกรรม', m: 'ยกเลิก "' + title + '" ใช่หรือไม่ กิจกรรมจะไม่แสดงต่อนักเรียน', b: 'ยกเลิกกิจกรรม' },
            Delete: { t: 'ลบกิจกรรม', m: 'ลบ "' + title + '" ใช่หรือไม่ การลบนี้ย้อนกลับไม่ได้', b: 'ลบกิจกรรม' }
        }[action];
        App.confirm({
            title: texts.t, message: texts.m, button: texts.b,
            onConfirm: function () {
                return App.post(App.url('Events/' + action + '/' + id))
                    .done(function (r) {
                        App.toast(r.message);
                        const d = bootstrap.Modal.getInstance(document.getElementById('eventDetailsModal'));
                        if (d) d.hide();
                        changed();
                    })
                    .fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
            }
        });
    }

    /* ---------- Modal รายละเอียด ---------- */
    const Details = {
        current: null,
        show: function (id) {
            App.getJson('Events/GetEvent/' + id).done(function (r) { Details.render(r.data); })
                .fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
        },
        render: function (e) {
            this.current = e;
            const $m = $('#eventDetailsModal');
            $m.toggleClass('manage-hidden', !e.canManage);
            $('#detailsTitle').text(e.title);

            const $b = $('#detailsBadges').empty();
            $('<span class="status-pill">').addClass('status-' + e.status.toLowerCase()).text(e.statusText).appendTo($b);
            const levelClass = { 'วิทยาลัย': 'college', 'แผนก': 'department', 'ห้องเรียน': 'classroom', 'บุคคล': 'individual' };
            e.levels.forEach(function (l) { $('<span class="level-pill">').addClass('level-' + levelClass[l]).text(l).appendTo($b); });

            $('#detailsDate').text(e.dateText);
            const $loc = $('#detailsLocation').text(e.locationDisplay);
            if (e.onlineUrl && /^https?:\/\//i.test(e.onlineUrl)) {
                $loc.append(' ').append($('<a target="_blank" rel="noopener noreferrer">').attr('href', e.onlineUrl).text('เปิดลิงก์'));
            }
            $('#detailsCategory').empty().append($('<span class="color-dot">').css('background', e.categoryColor)).append(document.createTextNode(e.categoryName));
            $('#detailsResponsible').text(e.responsibleName);
            $('#detailsPublish').text(e.publishAtText);
            $('#detailsUntil').text(e.displayUntilText);
            $('#detailsDescription').text(e.description || '').toggle(!!e.description);

            const t = e.targets, parts = [];
            if (t.college) parts.push('ทุกคนในวิทยาลัย');
            if (t.departments.length) parts.push('แผนก: ' + t.departments.map(function (x) { return x.name; }).join(', '));
            if (t.classrooms.length) parts.push('ห้องเรียน: ' + t.classrooms.map(function (x) { return x.name; }).join(', '));
            if (e.canManage && t.users.length) parts.push('บุคคล: ' + t.users.map(function (x) { return x.name; }).join(', '));
            else if (t.userCount) parts.push(t.users.length ? 'บุคคล: คุณและผู้เกี่ยวข้อง ' + t.userCount + ' คน' : 'บุคคลที่เกี่ยวข้อง ' + t.userCount + ' คน');
            const $targets = $('#detailsTargets').empty();
            parts.forEach(function (p) { $('<div>').text(p).appendTo($targets); });

            $('#detailsPublishBtn').toggle(e.status === 'Draft' || e.status === 'Scheduled');
            $('#detailsCancel').toggle(e.status !== 'Cancelled');
            bootstrap.Modal.getOrCreateInstance($m[0]).show();
        }
    };
    window.EventDetails = Details;

    $(document).on('click', '[data-event-details]', function () { Details.show($(this).data('event-details')); });
    $(document).on('click', '[data-event-action]', function () {
        const $b = $(this);
        runAction($b.data('event-action'), $b.data('id'), $b.attr('data-title'));
    });
    $(document).on('click', '#detailsPublishBtn', function () { runAction('Publish', Details.current.id, Details.current.title); });
    $(document).on('click', '#detailsCancel', function () { runAction('Cancel', Details.current.id, Details.current.title); });
    $(document).on('click', '#detailsDelete', function () { runAction('Delete', Details.current.id, Details.current.title); });
    $(document).on('click', '#detailsEdit', function () {
        const id = Details.current.id;
        bootstrap.Modal.getInstance(document.getElementById('eventDetailsModal')).hide();
        EventForm.edit(id);
    });

    /* ---------- Modal เพิ่ม/แก้ไข ---------- */
    const EventForm = {
        rooms: new Map(),
        users: new Map(),
        keepCollege: false,
        el: function (name) { return $('#Input_' + name); },

        reset: function () {
            const form = document.getElementById('eventForm');
            form.reset();
            this.el('Id').val('');
            this.rooms = new Map();
            this.users = new Map();
            this.keepCollege = false;
            $('#eventFormError, #targetError').addClass('d-none').text('');
            $('#tuSearch').val('');
            $('#tuList').empty();
            const validator = $(form).data('validator');
            if (validator) validator.resetForm();
            $(form).find('.input-validation-error').removeClass('input-validation-error');
            $(form).find('[data-valmsg-for]').addClass('field-validation-valid').removeClass('field-validation-error').empty();
        },

        add: function (date) {
            this.reset();
            $('#eventModalTitle').text('เพิ่มกิจกรรม');
            const today = date || new Date().toLocaleDateString('sv-SE');
            this.el('StartDate').val(today);
            this.el('EndDate').val(today);
            this.el('StartTime').val('09:00');
            this.el('EndTime').val('12:00');
            this.el('ResponsibleUserId').val($('#eventModal').data('current-user'));
            this.el('Status').val('Draft');
            this.sync();
            this.renderChips();
            this.loadRooms();
            this.searchUsers();
            bootstrap.Modal.getOrCreateInstance(document.getElementById('eventModal')).show();
        },

        edit: function (id) {
            App.getJson('Events/GetEvent/' + id).done(function (r) {
                EventForm.reset();
                EventForm.fill(r.data);
                bootstrap.Modal.getOrCreateInstance(document.getElementById('eventModal')).show();
            }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
        },

        fill: function (e) {
            $('#eventModalTitle').text('แก้ไขกิจกรรม');
            this.el('Id').val(e.id);
            this.el('Title').val(e.title);
            this.el('Description').val(e.description);
            this.el('ActivityCategoryId').val(String(e.activityCategoryId));
            // ประเภทหรือผู้รับผิดชอบที่ถูกปิดใช้งานแล้ว ต้องเห็นค่าเดิมในฟอร์ม
            if (this.el('ActivityCategoryId').val() !== String(e.activityCategoryId)) {
                this.el('ActivityCategoryId').append($('<option>').val(e.activityCategoryId).text(e.categoryName)).val(String(e.activityCategoryId));
            }
            this.el('ResponsibleUserId').val(e.responsibleUserId);
            if (this.el('ResponsibleUserId').val() !== e.responsibleUserId) {
                this.el('ResponsibleUserId').append($('<option>').val(e.responsibleUserId).text(e.responsibleName)).val(e.responsibleUserId);
            }
            this.el('StartDate').val(e.startDate);
            this.el('EndDate').val(e.endDate);
            this.el('IsAllDay').prop('checked', e.isAllDay);
            this.el('StartTime').val(e.startTime || '');
            this.el('EndTime').val(e.endTime || '');
            this.el('LocationId').val(e.locationId ? String(e.locationId) : '');
            this.el('LocationText').val(e.locationText);
            this.el('IsOnline').prop('checked', e.isOnline);
            this.el('OnlineUrl').val(e.onlineUrl);
            this.el('Status').val(e.status);
            this.el('PublishAt').val(e.publishAt ? e.publishAt.slice(0, 16) : '');
            this.el('DisplayUntil').val(e.displayUntil || '');

            const t = e.targets;
            // ครูไม่มีช่องเลือกระดับวิทยาลัย แต่ถ้าได้รับมอบหมายกิจกรรมระดับนี้ ต้องคงเป้าหมายเดิมไว้
            this.keepCollege = t.college;
            $('#targetCollege').prop('checked', t.college);
            $('.target-dept').prop('checked', false);
            t.departments.forEach(function (d) {
                const $c = $('#td' + d.id);
                if ($c.length) { $c.prop('checked', true); return; }
                // แผนกเดิมนอกความรับผิดชอบของครู คงไว้โดยแสดงเป็นรายการเลือกแล้ว
                const $wrap = $('<div class="form-check">');
                $('<input class="form-check-input target-dept" type="checkbox">').attr('id', 'td' + d.id).val(d.id).prop('checked', true).appendTo($wrap);
                $('<label class="form-check-label">').attr('for', 'td' + d.id).text(d.name).appendTo($wrap);
                $('#targetDepartments').append($wrap);
            });
            const self = this;
            t.classrooms.forEach(function (c) { self.rooms.set(String(c.id), c.name); });
            t.users.forEach(function (u) { self.users.set(u.id, u.name); });
            this.sync();
            this.renderChips();
            this.loadRooms();
            this.searchUsers();
        },

        sync: function () {
            const allDay = this.el('IsAllDay').prop('checked');
            $('#eventTimeRow').toggleClass('d-none', allDay);
            $('#eventOnlineRow').toggleClass('d-none', !this.el('IsOnline').prop('checked'));
        },

        loadRooms: function () {
            const self = this;
            App.getJson('Events/GetClassrooms', {
                yearId: $('#trYear').val() || null, levelId: $('#trLevel').val() || null,
                gradeId: $('#trGrade').val() || null, departmentId: $('#trDept').val() || null
            }).done(function (list) {
                const $l = $('#trList').empty();
                if (!list.length) { $l.append('<p class="text-muted-soft small mb-0 p-2">ไม่พบห้องเรียน</p>'); return; }
                const $all = $('<label class="pick-item fw-semibold"><input class="form-check-input m-0" type="checkbox" id="trAll"><span>เลือกทั้งหมดในรายการนี้</span></label>');
                $l.append($all);
                list.forEach(function (c) {
                    const $row = $('<label class="pick-item">');
                    $('<input class="form-check-input m-0 tr-room" type="checkbox">').val(c.id).attr('data-name', c.name).prop('checked', self.rooms.has(String(c.id))).appendTo($row);
                    $('<span>').text(c.name).appendTo($row);
                    $('<small>').text(c.departmentName).appendTo($row);
                    $l.append($row);
                });
            }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
        },

        searchUsers: function () {
            App.getJson('Events/SearchUsers', { role: $('#tuRole').val() || null, q: $('#tuSearch').val() || null }).done(function (list) {
                const $l = $('#tuList').empty();
                if (!list.length) { $l.append('<p class="text-muted-soft small mb-0 p-2">ไม่พบบุคคล</p>'); return; }
                list.forEach(function (u) {
                    const $b = $('<button type="button" class="pick-item tu-add">').attr('data-id', u.id).attr('data-name', u.name);
                    $('<i class="bi bi-plus-circle">').appendTo($b);
                    $('<span>').text(u.name).appendTo($b);
                    $('<small>').text(u.roleText + (u.detail ? ' · ' + u.detail : '')).appendTo($b);
                    $l.append($b);
                });
            }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
        },

        renderChips: function () {
            function draw($box, map, cls) {
                $box.empty();
                map.forEach(function (name, id) {
                    const $chip = $('<span class="chip">').text(name);
                    $('<button type="button" aria-label="นำออก">').addClass(cls).attr('data-id', id).html('<i class="bi bi-x"></i>').appendTo($chip);
                    $box.append($chip);
                });
            }
            draw($('#trChips'), this.rooms, 'tr-remove');
            draw($('#tuChips'), this.users, 'tu-remove');
        },

        collect: function () {
            const v = function (name) { const x = EventForm.el(name).val(); return x === '' || x == null ? null : x; };
            const allDay = this.el('IsAllDay').prop('checked');
            const online = this.el('IsOnline').prop('checked');
            return {
                id: v('Id') ? Number(v('Id')) : null,
                title: v('Title'),
                description: v('Description'),
                activityCategoryId: v('ActivityCategoryId') ? Number(v('ActivityCategoryId')) : null,
                responsibleUserId: v('ResponsibleUserId'),
                startDate: v('StartDate'),
                endDate: v('EndDate'),
                isAllDay: allDay,
                startTime: allDay ? null : v('StartTime'),
                endTime: allDay ? null : v('EndTime'),
                locationId: v('LocationId') ? Number(v('LocationId')) : null,
                locationText: v('LocationText'),
                isOnline: online,
                onlineUrl: online ? v('OnlineUrl') : null,
                status: v('Status'),
                publishAt: v('PublishAt'),
                displayUntil: v('DisplayUntil'),
                targets: {
                    college: $('#targetCollege').length ? $('#targetCollege').prop('checked') === true : this.keepCollege,
                    departmentIds: $('.target-dept:checked').map(function () { return Number(this.value); }).get(),
                    classroomIds: Array.from(this.rooms.keys()).map(Number),
                    userIds: Array.from(this.users.keys())
                }
            };
        },

        /** กฎข้ามฟิลด์ฝั่ง Client ตรงกับ EventInput.Validate ฝั่ง Server */
        crossValidate: function (d) {
            const errors = {};
            if (d.startDate && d.endDate && d.startDate > d.endDate) errors.EndDate = 'วันเริ่มต้องไม่เกินวันสิ้นสุด';
            if (!d.isAllDay) {
                if (!d.startTime) errors.StartTime = 'กรุณาระบุเวลาเริ่ม';
                if (!d.endTime) errors.EndTime = 'กรุณาระบุเวลาสิ้นสุด';
                if (d.startTime && d.endTime && d.startDate === d.endDate && d.startTime > d.endTime) errors.EndTime = 'เวลาเริ่มต้องไม่เกินเวลาสิ้นสุด';
            }
            if (!d.isOnline && !d.locationId && !(d.locationText || '').trim()) errors.LocationId = 'กรุณาระบุสถานที่ หรือเลือกว่าจัดแบบออนไลน์';
            if (d.status === 'Scheduled' && !d.publishAt) errors.PublishAt = 'สถานะตั้งเวลาเผยแพร่ต้องระบุวันเวลาเผยแพร่';
            if (d.displayUntil && d.publishAt && d.displayUntil < d.publishAt.slice(0, 10)) errors.DisplayUntil = 'วันที่สิ้นสุดการแสดงผลต้องไม่ก่อนวันเผยแพร่';
            const t = d.targets;
            if (!t.college && !t.departmentIds.length && !t.classroomIds.length && !t.userIds.length) errors.Targets = 'กรุณาเลือกกลุ่มเป้าหมายอย่างน้อยหนึ่งรายการ';
            return errors;
        },

        showErrors: function (errors) {
            $('#targetError').addClass('d-none').text('');
            let first = null;
            Object.keys(errors).forEach(function (key) {
                const name = key.charAt(0).toUpperCase() + key.slice(1);
                const text = Array.isArray(errors[key]) ? errors[key].join(' ') : errors[key];
                if (name === 'Targets' || name.indexOf('Targets.') === 0) {
                    $('#targetError').removeClass('d-none').text(text);
                    first = first || $('#targetError');
                    return;
                }
                const $msg = $('#eventForm [data-valmsg-for="Input.' + name + '"]');
                if ($msg.length) {
                    $msg.removeClass('field-validation-valid').addClass('field-validation-error').text(text);
                    first = first || $msg;
                } else {
                    $('#eventFormError').removeClass('d-none').text(text);
                    first = first || $('#eventFormError');
                }
            });
            if (first && first[0]) first[0].scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
    };
    window.EventForm = EventForm;

    $(function () {
        const $form = $('#eventForm');
        if (!$form.length) return;
        if ($.validator && $.validator.unobtrusive) {
            $.validator.unobtrusive.parse($form);
            // ช่องเวลาเมื่อเป็นกิจกรรมทั้งวันถูกซ่อน จึงไม่ต้องตรวจ
            $form.data('validator').settings.ignore = ':hidden';
        }

        EventForm.el('IsAllDay').on('change', function () { EventForm.sync(); });
        EventForm.el('IsOnline').on('change', function () { EventForm.sync(); });
        EventForm.el('StartDate').on('change', function () {
            const end = EventForm.el('EndDate');
            if (!end.val() || end.val() < this.value) end.val(this.value);
        });
        $('#trYear, #trLevel, #trGrade, #trDept').on('change', function () { EventForm.loadRooms(); });
        $('#tuRole').on('change', function () { EventForm.searchUsers(); });
        $('#tuSearch').on('input', App.debounce(function () { EventForm.searchUsers(); }, 300));

        $(document).on('change', '.tr-room', function () {
            if (this.checked) EventForm.rooms.set(String(this.value), $(this).attr('data-name'));
            else EventForm.rooms.delete(String(this.value));
            EventForm.renderChips();
            $('#targetError').addClass('d-none');
        });
        $(document).on('change', '#trAll', function () {
            const on = this.checked;
            $('.tr-room').each(function () { if (this.checked !== on) { this.checked = on; $(this).trigger('change'); } });
        });
        $(document).on('click', '.tr-remove', function () {
            const id = String($(this).attr('data-id'));
            EventForm.rooms.delete(id);
            $('.tr-room').filter(function () { return String(this.value) === id; }).prop('checked', false);
            EventForm.renderChips();
        });
        $(document).on('click', '.tu-add', function () {
            EventForm.users.set($(this).attr('data-id'), $(this).attr('data-name'));
            EventForm.renderChips();
            $('#targetError').addClass('d-none');
        });
        $(document).on('click', '.tu-remove', function () {
            EventForm.users.delete($(this).attr('data-id'));
            EventForm.renderChips();
        });

        $form.on('submit', function (ev) {
            ev.preventDefault();
            $('#eventFormError').addClass('d-none').text('');
            const clientValid = !$.validator || $form.valid();
            const data = EventForm.collect();
            const errors = EventForm.crossValidate(data);
            EventForm.showErrors(errors);
            if (!clientValid || Object.keys(errors).length) return;

            const $save = $('#eventSaveButton').prop('disabled', true);
            const path = data.id ? 'Events/Edit/' + data.id : 'Events/Create';
            App.postJson(path, data).done(function (r) {
                bootstrap.Modal.getInstance(document.getElementById('eventModal')).hide();
                App.toast(r.message);
                changed();
            }).fail(function (xhr) {
                const body = xhr.responseJSON;
                if (body && body.errors && Object.keys(body.errors).length) EventForm.showErrors(body.errors);
                if (!body || !body.errors || !Object.keys(body.errors).length) {
                    $('#eventFormError').removeClass('d-none').text(App.errorMessage(xhr));
                    $('#eventFormError')[0].scrollIntoView({ block: 'center' });
                } else {
                    App.toast(App.errorMessage(xhr), 'error');
                }
            }).always(function () { $save.prop('disabled', false); });
        });
    });

    $(document).on('click', '[data-add-event]', function () { EventForm.add(); });
    $(document).on('click', '[data-edit-event]', function () { EventForm.edit($(this).data('edit-event')); });

    /* ---------- รายการกิจกรรมแบบ AJAX ---------- */
    window.EventList = {
        page: 1,
        load: function () {
            const query = $('#eventFilterForm').serializeArray().filter(function (x) { return x.value !== ''; });
            query.push({ name: 'page', value: this.page });
            $.ajax({ url: App.url('Events/List'), type: 'GET', data: query, dataType: 'html' })
                .done(function (html) { $('#eventList').html(html); })
                .fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
        },
        init: function () {
            const self = this;
            const reload = function () { self.page = 1; self.load(); };
            const $form = $('#eventFilterForm');
            const dropPreset = function () {
                $form.find('input[type=hidden]').val('');
                $('#filterPreset').remove();
                history.replaceState(null, '', App.url('Events'));
            };
            $form.on('change', 'select, input[type=date]', function () {
                // เปลี่ยนวันที่หรือสถานะเอง ถือว่าไม่ได้ดูตามการ์ดแล้ว
                if (this.type === 'date' || this.name === 'status') dropPreset();
                reload();
            })
                .on('input', 'input[type=search]', App.debounce(reload, 300))
                .on('submit', function (e) { e.preventDefault(); reload(); })
                .on('reset', function () {
                    // reset ของเบราว์เซอร์คืนค่าตอนเปิดหน้า จึงต้องล้างเองให้ว่างจริง
                    window.setTimeout(function () {
                        $form.find('input').val('');
                        $form.find('select').val('');
                        dropPreset();
                        reload();
                    }, 0);
                });
            $('#presetClear').on('click', function () { $form.trigger('reset'); });
            $('#eventList').on('click', '[data-page]', function () {
                self.page = Number($(this).data('page'));
                self.load();
            });
            $(document).on('event:changed', function () { self.load(); });
        }
    };
})(jQuery);
