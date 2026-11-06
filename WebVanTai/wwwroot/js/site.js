// ============================================================
// site.js — JS dùng chung cho trang khách hàng
// ============================================================

// Navbar: đổ bóng nhẹ khi cuộn xuống để tạo chiều sâu
window.addEventListener('scroll', function () {
    var nav = document.querySelector('header .navbar');
    if (nav) {
        nav.classList.toggle('navbar-scrolled', window.scrollY > 8);
    }
}, { passive: true });
