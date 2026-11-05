/* ฟังก์ชันใช้ร่วมกันทั้งระบบ */
window.App = (function ($) {
    'use strict';
    const base = $('meta[name="app-base"]').attr('content') || '/';
    const token = $('meta[name="csrf-token"]').attr('content');
    const FLASH_KEY = 'app-flash';

    // แนบ Anti-forgery token ทุก AJAX ที่ไม่ใช่ GET
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            if (!/^(GET|HEAD|OPTIONS)$/i.test(settings.type)) xhr.setRequestHeader('RequestVerificationToken', token);
        }
    });

    function url(path) { return base + path.replace(/^\//, ''); }
    function esc(text) { return $('<div>').text(text == null ? '' : String(text)).html(); }

    function toast(message, type) {
        const kind = type || 'success';
        const icon = kind === 'success' ? 'bi-check-circle' : 'bi-exclamation-triangle';
        const $t = $('<div class="toast align-items-center border-0" role="status" aria-live="polite"><div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="ปิด"></button></div></div>');
        $t.addClass(kind === 'success' ? 'text-bg-success' : 'text-bg-danger');
        $t.find('.toast-body').append($('<i class="bi me-2">').addClass(icon)).append(document.createTextNode(message));
        $('#toastContainer').append($t);
        $t.on('hidden.bs.toast', function () { $t.remove(); });
        new bootstrap.Toast($t[0], { delay: kind === 'success' ? 3500 : 7000 }).show();
    }

    function errorMessage(xhr) {
        if (xhr.responseJSON && xhr.responseJSON.message) return xhr.responseJSON.message;
        switch (xhr.status) {
            case 0: return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาตรวจสอบอินเทอร์เน็ต';
            case 400: return 'ข้อมูลไม่ถูกต้อง หรือหน้าเว็บหมดอายุ กรุณาโหลดหน้าใหม่';
            case 401: return 'หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่';
            case 403: return 'คุณไม่มีสิทธิ์ดำเนินการนี้';
            case 404: return 'ไม่พบข้อมูลที่ต้องการ';
            default: return 'เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง';
        }
    }

    function handleUnauthorized(xhr) {
        if (xhr.status === 401) window.setTimeout(function () { location.href = url('Account/Login?returnUrl=' + encodeURIComponent(location.pathname)); }, 1200);
    }
    $(document).ajaxError(function (_e, xhr) { handleUnauthorized(xhr); });

    function postJson(path, data) {
        return $.ajax({ url: url(path), type: 'POST', contentType: 'application/json; charset=utf-8', dataType: 'json', data: JSON.stringify(data || {}) });
    }
    function post(fullUrl) { return $.ajax({ url: fullUrl, type: 'POST', dataType: 'json' }); }
    function getJson(path, data) { return $.ajax({ url: url(path), type: 'GET', dataType: 'json', data: data }); }

    /** ส่งข้อความไปแสดงหลัง reload หน้า */
    function flashAfterReload(message) {
        try { sessionStorage.setItem(FLASH_KEY, message); } catch (e) { /* เบราว์เซอร์ปิด storage */ }
        location.reload();
    }
    $(function () {
        try {
            const message = sessionStorage.getItem(FLASH_KEY);
            if (message) { sessionStorage.removeItem(FLASH_KEY); toast(message); }
        } catch (e) { /* ไม่มีผลต่อการทำงาน */ }
    });

    function confirmDialog(options) {
        const el = document.getElementById('confirmDeleteModal');
        const modal = bootstrap.Modal.getOrCreateInstance(el);
        $('#confirmDeleteTitle').text(options.title || 'ยืนยันการลบ');
        $('#confirmDeleteMessage').text(options.message || 'ต้องการดำเนินการนี้ใช่หรือไม่');
        const $btn = $('#confirmDeleteButton').text(options.button || 'ยืนยัน');
        $btn.off('click').on('click', function () {
            $btn.prop('disabled', true);
            $.when(options.onConfirm()).always(function () {
                $btn.prop('disabled', false);
                modal.hide();
            });
        });
        modal.show();
    }

    // ปุ่มลบ/ยืนยันทั่วไป: <button data-confirm-post="url" data-confirm-message="...">
    $(document).on('click', '[data-confirm-post]', function () {
        const $b = $(this);
        confirmDialog({
            title: $b.data('confirm-title') || 'ยืนยันการลบ',
            message: $b.data('confirm-message'),
            onConfirm: function () {
                return post($b.attr('data-confirm-post'))
                    .done(function (r) { flashAfterReload(r.message || 'ดำเนินการเรียบร้อยแล้ว'); })
                    .fail(function (xhr) { toast(errorMessage(xhr), 'error'); });
            }
        });
    });

    function debounce(fn, ms) {
        let timer;
        return function () {
            const args = arguments, ctx = this;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(ctx, args); }, ms);
        };
    }

    // ข้อความ jQuery Validation ภาษาไทย
    $(function () {
        if (!$.validator) return;
        $.extend($.validator.messages, {
            required: 'กรุณากรอกข้อมูล',
            email: 'รูปแบบอีเมลไม่ถูกต้อง',
            url: 'รูปแบบลิงก์ไม่ถูกต้อง',
            date: 'รูปแบบวันที่ไม่ถูกต้อง',
            number: 'กรุณากรอกเป็นตัวเลข',
            digits: 'กรุณากรอกเฉพาะตัวเลข',
            minlength: $.validator.format('กรุณากรอกอย่างน้อย {0} ตัวอักษร'),
            maxlength: $.validator.format('กรุณากรอกไม่เกิน {0} ตัวอักษร'),
            range: $.validator.format('กรุณากรอกค่าระหว่าง {0} ถึง {1}')
        });
    });

    return { url, esc, toast, errorMessage, postJson, post, getJson, confirm: confirmDialog, debounce, flashAfterReload };
})(jQuery);
