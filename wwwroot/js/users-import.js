/* นำเข้าครูและนักเรียนจาก Excel: ดาวน์โหลดแม่แบบ -> ตรวจสอบ -> ยืนยัน */
$(function () {
    'use strict';
    const MAX_BYTES = 5 * 1024 * 1024;
    const HINTS = {
        Student: 'นักเรียนหนึ่งคนต่อหนึ่งแถว ใส่ห้องเรียนแล้วเว้นแผนกและชั้นปีได้',
        Teacher: 'ครูที่ดูแลหลายขอบเขตให้กรอกหลายแถวด้วยอีเมลเดียวกัน เลือกแค่แผนก = ทั้งแผนก, แผนก + ชั้นปี = ทุกห้อง, ใส่ห้อง = เฉพาะห้องนั้น'
    };
    let role = $('#importForm').data('role');
    let credentials = null;

    function showError(text) { $('#importError').text(text).toggleClass('d-none', !text); }

    function resetResults() {
        showError('');
        $('#importPreview, #importDone').addClass('d-none');
    }

    function syncRole() {
        $('#importRole .nav-link').each(function () { $(this).toggleClass('active', $(this).data('role') === role); });
        $('#importHint').text(HINTS[role]);
        $('#importTemplate').attr('href', App.url('Users/ImportTemplate?role=' + role));
    }

    $('#importRole').on('click', '.nav-link', function () {
        role = $(this).data('role');
        syncRole();
        resetResults();
    });

    function clearFile() {
        $('#importFile').val('');
        $('#importCheck').prop('disabled', true);
    }

    // ให้เลือกไฟล์เดิมซ้ำได้หลังแก้ใน Excel ไม่อย่างนั้นเบราว์เซอร์จะไม่แจ้งว่ามีการเปลี่ยนแปลง
    $('#importFile').on('click', function () { this.value = ''; $('#importCheck').prop('disabled', true); });

    $('#importFile').on('change', function () {
        resetResults();
        const file = this.files[0];
        if (file && file.size > MAX_BYTES) {
            showError('ไฟล์ต้องมีขนาดไม่เกิน 5 MB');
            clearFile();
            return;
        }
        if (file && !/\.xlsx$/i.test(file.name)) {
            showError('รองรับเฉพาะไฟล์ .xlsx');
            clearFile();
            return;
        }
        $('#importCheck').prop('disabled', !file);
    });

    function uploadError(xhr) {
        // เบราว์เซอร์ส่งไฟล์ไม่ได้เมื่อไฟล์ถูกแก้ไขหลังจากเลือกไปแล้ว
        return xhr.status === 0 ? 'ส่งไฟล์ไม่สำเร็จ หากเพิ่งแก้ไฟล์ใน Excel กรุณาบันทึกแล้วเลือกไฟล์ใหม่อีกครั้ง' : App.errorMessage(xhr);
    }

    function upload(path) {
        const data = new FormData();
        data.append('file', $('#importFile')[0].files[0]);
        data.append('role', role);
        return $.ajax({ url: App.url(path), type: 'POST', data: data, processData: false, contentType: false, dataType: 'json' });
    }

    function renderPreview(p) {
        $('#previewTitle').text('ตรวจสอบ' + p.roleText + ' ' + p.total.toLocaleString() + ' คน: ถูกต้อง ' + p.validCount.toLocaleString() + ' · มีข้อผิดพลาด ' + p.errorCount.toLocaleString());
        $('#previewBlocked').toggleClass('d-none', p.errorCount === 0)
            .text(p.errorCount ? 'นำเข้าไม่ได้จนกว่าจะแก้ครบทุกรายการ แก้ไฟล์ Excel แล้วเลือกไฟล์ใหม่อีกครั้ง ยังไม่มีการบันทึกข้อมูล' : '');
        $('#importConfirm').prop('disabled', p.errorCount > 0);

        const $body = $('#previewRows').empty();
        // แสดงรายการที่ผิดก่อน จะได้ไม่ต้องไล่หา
        p.rows.slice().sort(function (a, b) { return b.errors.length - a.errors.length; }).forEach(function (r) {
            const $tr = $('<tr>').toggleClass('import-row-error', r.errors.length > 0);
            $('<td>').text(r.rows).appendTo($tr);
            $('<td class="fw-semibold">').text(r.fullName).appendTo($tr);
            $('<td class="text-break">').text(r.email).appendTo($tr);
            $('<td>').text(r.code).appendTo($tr);
            $('<td>').text(r.detail).appendTo($tr);
            $('<td class="text-nowrap">').text(r.passwordText).appendTo($tr);
            const $check = $('<td>').appendTo($tr);
            if (r.errors.length) r.errors.forEach(function (e) { $('<div class="invalid-text">').text(e).appendTo($check); });
            else $('<span class="status-pill status-published">').text('พร้อมนำเข้า').appendTo($check);
            $body.append($tr);
        });
        $('#importPreview').removeClass('d-none');
    }

    $('#importCheck').on('click', function () {
        const $btn = $(this).prop('disabled', true);
        resetResults();
        upload('Users/ImportPreview').done(function (r) { renderPreview(r.data); })
            .fail(function (xhr) { showError(uploadError(xhr)); })
            .always(function () { $btn.prop('disabled', false); });
    });

    function downloadCredentials() {
        if (!credentials) return;
        const bytes = Uint8Array.from(atob(credentials.base64), function (c) { return c.charCodeAt(0); });
        const blob = new Blob([bytes], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = credentials.name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    }

    $('#credentialsDownload').on('click', downloadCredentials);

    $('#importConfirm').on('click', function () {
        App.confirm({
            title: 'ยืนยันการนำเข้า',
            message: 'สร้างบัญชีตามไฟล์นี้ใช่หรือไม่ หากต้องการลบภายหลังต้องลบทีละบัญชี',
            button: 'นำเข้า',
            onConfirm: function () {
                return upload('Users/ImportConfirm').done(function (r) {
                    $('#importPreview').addClass('d-none');
                    $('#doneTitle').text(r.message);
                    $('#doneList').attr('href', App.url('Users?role=' + role));
                    const d = r.data;
                    credentials = d.credentialsFileBase64 ? { base64: d.credentialsFileBase64, name: d.credentialsFileName } : null;
                    $('#doneCredentials').toggleClass('d-none', !credentials);
                    $('#credentialsText').text(credentials ? 'ระบบสุ่มรหัสผ่านให้ ' + d.generatedPasswords.toLocaleString() + ' คน' : '');
                    $('#importDone').removeClass('d-none');
                    clearFile();
                    App.toast(r.message);
                }).fail(function (xhr) {
                    $('#importPreview').addClass('d-none');
                    showError(uploadError(xhr));
                });
            }
        });
    });

    $('#doneAgain').on('click', function () {
        credentials = null;
        resetResults();
    });

    syncRole();
});
