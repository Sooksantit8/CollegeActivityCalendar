/* ผู้ใช้งาน: ครูและนักเรียน กรองรายการ, ดรอปดาวน์ แผนก > ชั้นปี > ห้อง และขอบเขตที่ครูดูแล */
$(function () {
    'use strict';

    /* ---------- แผนก > ชั้นปี > ห้อง ---------- */
    function loadRooms($scope) {
        const $room = $scope.find('[data-cascade="room"]');
        if (!$room.length) return $.Deferred().resolve();
        const selected = $room.attr('data-selected') || $room.val() || '';
        return App.getJson('Users/GetClassrooms', {
            departmentId: $scope.find('[data-cascade="dept"]').val() || null,
            gradeId: $scope.find('[data-cascade="grade"]').val() || null,
            includeId: selected || null
        }).done(function (list) {
            const $first = $room.find('option').first().detach();
            $room.empty().append($first);
            list.forEach(function (c) {
                $('<option>').val(c.id).text(c.name).attr({ 'data-dept': c.departmentId, 'data-grade': c.gradeLevelId }).appendTo($room);
            });
            $room.val(selected);
            if ($room.val() == null) $room.val('');
            $room.removeAttr('data-selected');
        }).fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
    }

    $(document).on('change', '.room-cascade [data-cascade="dept"], .room-cascade [data-cascade="grade"]', function () {
        const $scope = $(this).closest('.room-cascade');
        $scope.find('[data-cascade="room"]').val('');
        loadRooms($scope);
    });
    // เลือกห้องแล้วให้แผนกและชั้นปีตรงกับห้องนั้น
    $(document).on('change', '.room-cascade [data-cascade="room"]', function () {
        const $opt = $(this).find('option:selected');
        if (!$opt.val()) return;
        const $scope = $(this).closest('.room-cascade');
        $scope.find('[data-cascade="dept"]').val(String($opt.attr('data-dept')));
        $scope.find('[data-cascade="grade"]').val(String($opt.attr('data-grade')));
    });
    $('.room-cascade').each(function () { loadRooms($(this)); });

    /* ---------- กรองรายการผู้ใช้ ---------- */
    const $filter = $('#userFilterForm');
    if ($filter.length) {
        let page = 1;
        const load = function () {
            const query = $filter.serializeArray().filter(function (x) { return x.value !== ''; });
            query.push({ name: 'page', value: page });
            $.ajax({ url: App.url('Users/List'), type: 'GET', data: query, dataType: 'html' })
                .done(function (html) { $('#userList').html(html); })
                .fail(function (xhr) { App.toast(App.errorMessage(xhr), 'error'); });
        };
        const reload = function () { page = 1; load(); };
        $filter.on('change', 'select', function () { window.setTimeout(reload, 0); })
            .on('input', 'input[type=search]', App.debounce(reload, 300))
            .on('submit', function (e) { e.preventDefault(); reload(); });
        $('#userList').on('click', '[data-page]', function () { page = Number($(this).data('page')); load(); });
        $('#activeClear').on('click', function () {
            $('#uActive').val('');
            $('#activePreset').remove();
            history.replaceState(null, '', App.url('Users' + ($('#uRole').val() ? '?role=' + $('#uRole').val() : '')));
            reload();
        });
        $('#userRoleTabs').on('click', 'a', function (e) {
            e.preventDefault();
            const role = $(this).data('role') || '';
            $('#userRoleTabs a').removeClass('active');
            $(this).addClass('active');
            $('#uRole').val(role);
            $('#addUserLink').attr('href', App.url('Users/Create' + (role ? '?role=' + role : '')));
            $('#importUserLink').attr('href', App.url('Users/Import?role=' + (role === 'Teacher' ? 'Teacher' : 'Student')));
            history.replaceState(null, '', this.href);
            reload();
        });
    }

    /* ---------- ฟอร์มผู้ใช้: สลับฟิลด์ตามบทบาท ---------- */
    const $role = $('#roleValue');
    if ($role.length) {
        const sync = function () {
            const role = $role.val();
            $('.role-section').each(function () {
                const on = String($(this).data('roles')).split(',').indexOf(role) >= 0;
                $(this).toggleClass('d-none', !on);
                // ฟิลด์ที่ซ่อนต้องไม่ถูกส่ง จะได้ไม่ทับค่าของบทบาทอื่น
                $(this).find('select, input').prop('disabled', !on);
            });
            $('.staff-dept').attr('name', role === 'Staff' ? 'Input.DepartmentId' : null);
            if (role === 'Teacher' && !$('#assignmentRows .assignment-row').length) addAssignment();
        };
        $role.on('change', sync);

        /* ---------- ขอบเขตที่ครูดูแล ---------- */
        const $rows = $('#assignmentRows');
        const reindex = function () {
            $rows.find('.assignment-row').each(function (i) {
                $(this).find('[data-field]').each(function () {
                    $(this).attr('name', 'Input.Assignments[' + i + '].' + $(this).data('field'));
                });
            });
        };
        function addAssignment(values) {
            const $row = $($('#assignmentRowTemplate').html());
            $rows.append($row);
            if (values) {
                $row.find('[data-field="DepartmentId"]').val(String(values.DepartmentId || ''));
                $row.find('[data-field="GradeLevelId"]').val(values.GradeLevelId ? String(values.GradeLevelId) : '');
                if (values.ClassroomId) $row.find('[data-field="ClassroomId"]').attr('data-selected', values.ClassroomId);
            }
            reindex();
            loadRooms($row);
        }
        (function () {
            let existing = [];
            try { existing = JSON.parse($rows.attr('data-assignments') || '[]'); } catch (e) { existing = []; }
            existing.forEach(addAssignment);
        })();
        $('#addAssignment').on('click', function () { addAssignment(); });
        $rows.on('click', '.remove-assignment', function () {
            $(this).closest('.assignment-row').remove();
            reindex();
        });
        sync();
    }
});
