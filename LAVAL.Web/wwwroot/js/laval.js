const navLinks = Array.from(document.querySelectorAll('.nav-link[href^="#"]'));

document.querySelectorAll('a[href^="#"]').forEach((link) => {
    link.addEventListener("click", (event) => {
        const href = link.getAttribute("href");
        if (!href || href === "#") return;
        const target = document.querySelector(href);
        if (!target) return;
        event.preventDefault();
        target.scrollIntoView({ behavior: "smooth", block: "start" });
    });
});

const sections = navLinks
    .map((link) => document.querySelector(link.getAttribute("href")))
    .filter(Boolean);

function setActiveLink() {
    const y = window.scrollY + 140;
    let currentId = "";

    sections.forEach((section) => {
        if (y >= section.offsetTop) currentId = section.id;
    });

    navLinks.forEach((link) => {
        const isCurrent = link.getAttribute("href") === `#${currentId}`;
        link.classList.toggle("is-active", isCurrent);
    });
}

window.addEventListener("scroll", setActiveLink, { passive: true });
window.addEventListener("load", setActiveLink);
