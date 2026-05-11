(function () {
    const memberDiscountMap = {
        Silver: 5,
        Gold: 10,
        Platinum: 15
    };

    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(";").shift();
        }

        return null;
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat("id-ID", {
            style: "currency",
            currency: "IDR",
            maximumFractionDigits: 0
        }).format(value);
    }

    function escapeHtml(value) {
        if (!value) {
            return "";
        }

        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function showToast(message, type) {
        const container = document.getElementById("customer-toast-container");
        if (!container) {
            return;
        }

        const tone = type || "info";
        const icon = tone === "success"
            ? "fa-circle-check"
            : tone === "warning"
                ? "fa-circle-exclamation"
                : tone === "error"
                    ? "fa-circle-xmark"
                    : "fa-circle-info";

        const toast = document.createElement("div");
        toast.className = `customer-toast is-${tone}`;
        toast.innerHTML = `<i class="fas ${icon}"></i><span>${escapeHtml(message)}</span>`;
        container.appendChild(toast);

        window.setTimeout(function () {
            toast.remove();
        }, 3600);
    }

    function syncNavSearchToElement(input, callback) {
        if (!input || typeof callback !== "function") {
            return;
        }

        input.addEventListener("input", function () {
            callback(input.value || "");
        });
    }

    function initMenuPage() {
        const app = document.getElementById("customer-order-app");
        if (!app) {
            return;
        }

        const config = JSON.parse(app.dataset.config || "{}");
        const storageKey = `ceria-order-cart-${config.tableId || "default"}`;
        let lineCounter = Date.now();
        let cart = [];
        let activeCategory = "all";
        let selectedPaymentMethod = null;
        let productModal = null;
        let activeProduct = null;
        let modalQuantity = 1;

        const categoryButtons = Array.from(document.querySelectorAll("[data-category-filter]"));
        const menuCards = Array.from(document.querySelectorAll("[data-menu-card]"));
        const categorySections = Array.from(document.querySelectorAll("[data-menu-category]"));
        const noResultState = document.getElementById("catalog-empty-state");
        const pageSearch = document.getElementById("catalog-search");
        const navSearch = document.getElementById("customer-nav-search");
        const drawer = document.getElementById("cart-drawer");
        const overlay = document.getElementById("cart-overlay");
        const cartToggle = document.getElementById("customer-cart-toggle");
        const cartClose = document.getElementById("cart-drawer-close");
        const summaryList = document.getElementById("summary-item-list");
        const drawerList = document.getElementById("drawer-item-list");
        const summaryEmpty = document.getElementById("summary-empty-state");
        const drawerEmpty = document.getElementById("drawer-empty-state");
        const cartCount = document.getElementById("customer-cart-count");
        const summarySubtotal = document.getElementById("summary-subtotal");
        const summaryTax = document.getElementById("summary-tax");
        const summaryService = document.getElementById("summary-service");
        const summaryDiscount = document.getElementById("summary-discount");
        const summaryDiscountLabel = document.getElementById("summary-discount-label");
        const summaryTotal = document.getElementById("summary-total");
        const drawerSubtotal = document.getElementById("drawer-subtotal");
        const drawerTax = document.getElementById("drawer-tax");
        const drawerService = document.getElementById("drawer-service");
        const drawerDiscount = document.getElementById("drawer-discount");
        const drawerDiscountLabel = document.getElementById("drawer-discount-label");
        const drawerTotal = document.getElementById("drawer-total");
        const summaryNotes = document.getElementById("summary-discount-note");
        const drawerNotes = document.getElementById("drawer-discount-note");
        const drawerItemCount = document.getElementById("drawer-item-count");
        const summaryConfirm = document.getElementById("summary-confirm-order");
        const drawerConfirm = document.getElementById("drawer-confirm-order");
        const summaryBrowse = document.getElementById("summary-browse-menu");
        const drawerBrowseButtons = Array.from(document.querySelectorAll("[data-browse-menu]"));
        const summaryPrompts = Array.from(document.querySelectorAll("[data-payment-selector='summary']"));
        const drawerPrompts = Array.from(document.querySelectorAll("[data-payment-selector='drawer']"));
        const productModalElement = document.getElementById("product-detail-modal");
        const modalTitle = document.getElementById("modal-product-title");
        const modalDescription = document.getElementById("modal-product-description");
        const modalPrice = document.getElementById("modal-product-price");
        const modalBadge = document.getElementById("modal-product-status");
        const modalImage = document.getElementById("modal-product-image");
        const modalQty = document.getElementById("modal-product-qty");
        const modalNotes = document.getElementById("modal-product-notes");
        const modalMinus = document.getElementById("modal-qty-minus");
        const modalPlus = document.getElementById("modal-qty-plus");
        const modalAdd = document.getElementById("modal-add-to-cart");
        const antiForgery = document.querySelector("input[name='__RequestVerificationToken']");

        if (productModalElement) {
            productModal = new bootstrap.Modal(productModalElement);
        }

        function getMemberDiscountRate() {
            if (getCookie("IsMember") !== "true") {
                return 0;
            }

            const memberType = getCookie("MemberType");
            return memberDiscountMap[memberType] || 0;
        }

        function buildCartSummary() {
            const subtotal = cart.reduce(function (sum, item) {
                return sum + (item.price * item.quantity);
            }, 0);
            const memberDiscount = subtotal * (getMemberDiscountRate() / 100);
            const discountedBase = Math.max(0, subtotal - memberDiscount);
            let promoName = "";
            let promoDiscount = 0;

            (config.promos || []).forEach(function (promo) {
                if (subtotal < promo.minimumPurchase) {
                    return;
                }

                const currentDiscount = discountedBase * (promo.discountPercentage / 100);
                if (currentDiscount > promoDiscount) {
                    promoDiscount = currentDiscount;
                    promoName = promo.name;
                }
            });

            const discount = memberDiscount + promoDiscount;
            const tax = subtotal * ((config.taxRate || 0) / 100);
            const serviceCharge = subtotal * ((config.serviceChargeRate || 0) / 100);

            return {
                subtotal: subtotal,
                memberDiscount: memberDiscount,
                promoDiscount: promoDiscount,
                promoName: promoName,
                discount: discount,
                tax: tax,
                serviceCharge: serviceCharge,
                total: subtotal + tax + serviceCharge - discount
            };
        }

        function persistCartState() {
            const snapshot = {
                cart: cart,
                paymentMethod: selectedPaymentMethod
            };

            window.sessionStorage.setItem(storageKey, JSON.stringify(snapshot));
        }

        function restoreCartState() {
            const snapshot = window.sessionStorage.getItem(storageKey);
            if (!snapshot) {
                return;
            }

            try {
                const data = JSON.parse(snapshot);
                cart = Array.isArray(data.cart) ? data.cart : [];
                selectedPaymentMethod = data.paymentMethod || selectedPaymentMethod;
            } catch {
                cart = [];
            }
        }

        function createLineItem(product, quantity, notes) {
            lineCounter += 1;
            return {
                lineId: lineCounter,
                productId: product.productId,
                productName: product.productName,
                description: product.description,
                imageUrl: product.imageUrl,
                price: product.price,
                quantity: quantity,
                notes: notes || ""
            };
        }

        function addItemToCart(product, quantity, notes) {
            const normalizedNotes = (notes || "").trim();
            const existingItem = cart.find(function (item) {
                return item.productId === product.productId && (item.notes || "").trim() === normalizedNotes;
            });

            if (existingItem) {
                existingItem.quantity += quantity;
            } else {
                cart.push(createLineItem(product, quantity, normalizedNotes));
            }

            renderCart();
            persistCartState();
        }

        function setDrawerState(isOpen) {
            if (!drawer || !overlay) {
                return;
            }

            drawer.classList.toggle("is-open", isOpen);
            overlay.classList.toggle("is-open", isOpen);
            document.body.classList.toggle("customer-drawer-open", isOpen);
        }

        function applyFilter(term) {
            const normalized = (term || "").toLowerCase().trim();
            let visibleCount = 0;

            categorySections.forEach(function (section) {
                const sectionCategory = section.dataset.menuCategory;
                const categoryMatch = activeCategory === "all" || sectionCategory === activeCategory;
                let hasVisibleCards = false;

                Array.from(section.querySelectorAll("[data-menu-card]")).forEach(function (card) {
                    const name = (card.dataset.productName || "").toLowerCase();
                    const description = (card.dataset.productDescription || "").toLowerCase();
                    const textMatch = !normalized || name.includes(normalized) || description.includes(normalized);
                    const shouldShow = categoryMatch && textMatch;

                    card.parentElement.classList.toggle("is-hidden", !shouldShow);
                    if (shouldShow) {
                        hasVisibleCards = true;
                        visibleCount += 1;
                    }
                });

                section.classList.toggle("is-hidden", !hasVisibleCards);
            });

            if (noResultState) {
                noResultState.classList.toggle("is-hidden", visibleCount > 0);
            }
        }

        function syncPaymentMethod(value) {
            selectedPaymentMethod = value;
            summaryPrompts.concat(drawerPrompts).forEach(function (selector) {
                selector.checked = String(selector.value) === String(value);
            });
            persistCartState();
        }

        function renderSummaryItems(target) {
            if (!target) {
                return;
            }

            target.innerHTML = cart.map(function (item) {
                return `
                    <article class="summary-line-item">
                        <div class="summary-line-item__thumb">
                            ${item.imageUrl ? `<img src="${escapeHtml(item.imageUrl)}" alt="${escapeHtml(item.productName)}">` : ""}
                        </div>
                        <div class="summary-line-item__content">
                            <strong>${escapeHtml(item.productName)}</strong>
                            <span>${item.quantity}x • ${formatCurrency(item.price)}</span>
                            ${item.notes ? `<small>${escapeHtml(item.notes)}</small>` : ""}
                        </div>
                        <div class="summary-line-item__price">${formatCurrency(item.price * item.quantity)}</div>
                    </article>
                `;
            }).join("");
        }

        function renderDrawerItems() {
            if (!drawerList) {
                return;
            }

            drawerList.innerHTML = cart.map(function (item) {
                return `
                    <article class="drawer-item-card">
                        <div class="drawer-item-card__thumb">
                            ${item.imageUrl ? `<img src="${escapeHtml(item.imageUrl)}" alt="${escapeHtml(item.productName)}">` : ""}
                        </div>
                        <div class="drawer-item-card__content">
                            <div class="drawer-item-card__price-row">
                                <strong>${escapeHtml(item.productName)}</strong>
                                <strong>${formatCurrency(item.price * item.quantity)}</strong>
                            </div>
                            <span>${formatCurrency(item.price)} per item</span>
                            <textarea class="drawer-inline-note" data-note-input="${item.lineId}" placeholder="Catatan item...">${escapeHtml(item.notes)}</textarea>
                            <div class="drawer-item-card__controls">
                                <div class="drawer-stepper">
                                    <button type="button" data-qty-action="${item.lineId}" data-change="-1" aria-label="Kurangi jumlah">
                                        <i class="fas fa-minus"></i>
                                    </button>
                                    <strong>${item.quantity}</strong>
                                    <button type="button" data-qty-action="${item.lineId}" data-change="1" aria-label="Tambah jumlah">
                                        <i class="fas fa-plus"></i>
                                    </button>
                                </div>
                                <button type="button" class="drawer-remove-button" data-remove-line="${item.lineId}" aria-label="Hapus item">
                                    <i class="fas fa-trash"></i>
                                </button>
                            </div>
                        </div>
                    </article>
                `;
            }).join("");
        }

        function renderCart() {
            const itemCount = cart.reduce(function (sum, item) {
                return sum + item.quantity;
            }, 0);
            const summary = buildCartSummary();

            if (cartCount) {
                cartCount.textContent = String(itemCount);
                cartCount.classList.toggle("is-hidden", itemCount === 0);
            }

            if (summaryEmpty) {
                summaryEmpty.hidden = cart.length > 0;
            }

            if (drawerEmpty) {
                drawerEmpty.hidden = cart.length > 0;
            }

            if (summaryList) {
                summaryList.hidden = cart.length === 0;
            }

            if (drawerList) {
                drawerList.hidden = cart.length === 0;
            }

            renderSummaryItems(summaryList);
            renderDrawerItems();

            if (drawerItemCount) {
                drawerItemCount.textContent = itemCount === 0 ? "Belum ada item" : `${itemCount} item dalam keranjang`;
            }

            if (summarySubtotal) {
                summarySubtotal.textContent = formatCurrency(summary.subtotal);
            }
            if (summaryTax) {
                summaryTax.textContent = formatCurrency(summary.tax);
            }
            if (summaryService) {
                summaryService.textContent = formatCurrency(summary.serviceCharge);
            }
            if (summaryDiscount) {
                summaryDiscount.textContent = formatCurrency(summary.discount);
            }
            if (summaryTotal) {
                summaryTotal.textContent = formatCurrency(summary.total);
            }

            if (drawerSubtotal) {
                drawerSubtotal.textContent = formatCurrency(summary.subtotal);
            }
            if (drawerTax) {
                drawerTax.textContent = formatCurrency(summary.tax);
            }
            if (drawerService) {
                drawerService.textContent = formatCurrency(summary.serviceCharge);
            }
            if (drawerDiscount) {
                drawerDiscount.textContent = formatCurrency(summary.discount);
            }
            if (drawerTotal) {
                drawerTotal.textContent = formatCurrency(summary.total);
            }

            const note = summary.discount === 0
                ? "Diskon akan muncul otomatis jika syarat member atau promo terpenuhi."
                : summary.promoName
                    ? `Promo terbaik yang dipilih: ${summary.promoName}`
                    : "Diskon member aktif diterapkan otomatis.";

            if (summaryNotes) {
                summaryNotes.textContent = note;
            }
            if (drawerNotes) {
                drawerNotes.textContent = note;
            }

            const discountLabel = summary.promoName
                ? `Diskon (${summary.promoName})`
                : "Diskon";

            if (summaryDiscountLabel) {
                summaryDiscountLabel.textContent = discountLabel;
            }
            if (drawerDiscountLabel) {
                drawerDiscountLabel.textContent = discountLabel;
            }

            const disabled = cart.length === 0;
            if (summaryConfirm) {
                summaryConfirm.disabled = disabled;
            }
            if (drawerConfirm) {
                drawerConfirm.disabled = disabled;
            }
        }

        function updateQuantity(lineId, change) {
            const item = cart.find(function (entry) {
                return String(entry.lineId) === String(lineId);
            });

            if (!item) {
                return;
            }

            item.quantity += change;
            if (item.quantity <= 0) {
                cart = cart.filter(function (entry) {
                    return entry.lineId !== item.lineId;
                });
            }

            renderCart();
            persistCartState();
        }

        function updateLineNotes(lineId, value) {
            const item = cart.find(function (entry) {
                return String(entry.lineId) === String(lineId);
            });

            if (!item) {
                return;
            }

            item.notes = value;
            renderSummaryItems(summaryList);
            persistCartState();
        }

        function openProductModal(product) {
            if (!productModal || !product) {
                return;
            }

            activeProduct = product;
            modalQuantity = 1;
            modalTitle.textContent = product.productName;
            modalDescription.textContent = product.description || "Menu favorit Ceria Resto siap disajikan hangat di meja Anda.";
            modalPrice.textContent = formatCurrency(product.price);
            modalBadge.textContent = product.isAvailable ? "Tersedia" : "Tidak tersedia";
            modalBadge.className = `availability-badge ${product.isAvailable ? "" : "is-unavailable"}`;
            modalQty.textContent = String(modalQuantity);
            modalNotes.value = "";
            modalAdd.disabled = !product.isAvailable;
            modalImage.innerHTML = product.imageUrl
                ? `<img src="${escapeHtml(product.imageUrl)}" alt="${escapeHtml(product.productName)}">`
                : `<div class="customer-product-modal__image d-flex align-items-center justify-content-center"><i class="fas fa-bowl-food fa-3x text-secondary"></i></div>`;

            productModal.show();
        }

        function parseProductFromCard(card) {
            return {
                productId: Number(card.dataset.productId),
                productName: card.dataset.productName || "",
                description: card.dataset.productDescription || "",
                imageUrl: card.dataset.productImage || "",
                price: Number(card.dataset.productPrice),
                isAvailable: String(card.dataset.productAvailable).toLowerCase() === "true"
            };
        }

        function submitOrder(button) {
            if (cart.length === 0 || !selectedPaymentMethod || !antiForgery) {
                showToast("Keranjang masih kosong atau metode pembayaran belum dipilih.", "warning");
                return;
            }

            const originalText = button.innerHTML;
            button.disabled = true;
            button.innerHTML = "<i class='fas fa-spinner fa-spin'></i> Memproses...";

            const memberIdValue = getCookie("IsMember") === "true" ? parseInt(getCookie("UserId"), 10) : NaN;
            const payload = {
                tableId: Number(config.tableId),
                customerName: getCookie("Username") || "Guest",
                isMember: getCookie("IsMember") === "true",
                memberId: Number.isInteger(memberIdValue) ? memberIdValue : null,
                paymentMethod: Number(selectedPaymentMethod),
                items: cart.map(function (item) {
                    return {
                        productId: item.productId,
                        quantity: item.quantity,
                        notes: item.notes || ""
                    };
                })
            };

            fetch(config.orderCreateUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": antiForgery.value
                },
                body: JSON.stringify(payload)
            })
                .then(function (response) { return response.json(); })
                .then(function (result) {
                    if (!result.success) {
                        showToast(result.message || "Pesanan gagal diproses.", "error");
                        return;
                    }

                    cart = [];
                    persistCartState();
                    renderCart();
                    setDrawerState(false);
                    window.sessionStorage.removeItem(storageKey);
                    showToast(`Pesanan ${result.transactionNumber} berhasil dibuat.`, "success");
                    window.location.href = result.trackingUrl;
                })
                .catch(function (error) {
                    showToast(`Terjadi kesalahan: ${error.message}`, "error");
                })
                .finally(function () {
                    button.disabled = false;
                    button.innerHTML = originalText;
                });
        }

        function bindPaymentSelectors() {
            summaryPrompts.concat(drawerPrompts).forEach(function (selector) {
                selector.addEventListener("change", function () {
                    syncPaymentMethod(selector.value);
                });
            });
        }

        function initInteractions() {
            restoreCartState();

            categoryButtons.forEach(function (button) {
                button.addEventListener("click", function () {
                    activeCategory = button.dataset.categoryFilter || "all";
                    categoryButtons.forEach(function (chip) {
                        chip.classList.toggle("is-active", chip === button);
                    });

                    applyFilter(pageSearch ? pageSearch.value : navSearch ? navSearch.value : "");
                });
            });

            menuCards.forEach(function (card) {
                card.addEventListener("click", function (event) {
                    if (event.target.closest("[data-quick-add]")) {
                        return;
                    }

                    const product = parseProductFromCard(card);
                    if (!product.isAvailable) {
                        return;
                    }

                    openProductModal(product);
                });

                const quickAddButton = card.querySelector("[data-quick-add]");
                if (quickAddButton) {
                    quickAddButton.addEventListener("click", function (event) {
                        event.stopPropagation();
                        const product = parseProductFromCard(card);
                        if (!product.isAvailable) {
                            return;
                        }

                        addItemToCart(product, 1, "");
                        showToast(`${product.productName} ditambahkan ke keranjang.`, "success");
                    });
                }
            });

            if (modalMinus) {
                modalMinus.addEventListener("click", function () {
                    modalQuantity = Math.max(1, modalQuantity - 1);
                    modalQty.textContent = String(modalQuantity);
                });
            }

            if (modalPlus) {
                modalPlus.addEventListener("click", function () {
                    modalQuantity += 1;
                    modalQty.textContent = String(modalQuantity);
                });
            }

            if (modalAdd) {
                modalAdd.addEventListener("click", function () {
                    if (!activeProduct) {
                        return;
                    }

                    addItemToCart(activeProduct, modalQuantity, modalNotes.value);
                    productModal.hide();
                    showToast(`${activeProduct.productName} masuk ke keranjang.`, "success");
                });
            }

            syncNavSearchToElement(pageSearch, applyFilter);
            syncNavSearchToElement(navSearch, function (value) {
                if (pageSearch) {
                    pageSearch.value = value;
                }
                applyFilter(value);
            });

            if (cartToggle) {
                cartToggle.addEventListener("click", function () {
                    setDrawerState(true);
                });
            }

            if (cartClose) {
                cartClose.addEventListener("click", function () {
                    setDrawerState(false);
                });
            }

            if (overlay) {
                overlay.addEventListener("click", function () {
                    setDrawerState(false);
                });
            }

            document.addEventListener("keydown", function (event) {
                if (event.key === "Escape") {
                    setDrawerState(false);
                }
            });

            if (summaryBrowse) {
                summaryBrowse.addEventListener("click", function () {
                    document.getElementById("order-catalog")?.scrollIntoView({ behavior: "smooth", block: "start" });
                });
            }

            drawerBrowseButtons.forEach(function (drawerBrowse) {
                drawerBrowse.addEventListener("click", function () {
                    setDrawerState(false);
                    document.getElementById("order-catalog")?.scrollIntoView({ behavior: "smooth", block: "start" });
                });
            });

            if (summaryConfirm) {
                summaryConfirm.addEventListener("click", function () {
                    submitOrder(summaryConfirm);
                });
            }

            if (drawerConfirm) {
                drawerConfirm.addEventListener("click", function () {
                    submitOrder(drawerConfirm);
                });
            }

            if (drawerList) {
                drawerList.addEventListener("click", function (event) {
                    const qtyButton = event.target.closest("[data-qty-action]");
                    const removeButton = event.target.closest("[data-remove-line]");

                    if (qtyButton) {
                        updateQuantity(qtyButton.dataset.qtyAction, Number(qtyButton.dataset.change));
                    }

                    if (removeButton) {
                        cart = cart.filter(function (entry) {
                            return String(entry.lineId) !== String(removeButton.dataset.removeLine);
                        });
                        renderCart();
                        persistCartState();
                    }
                });

                drawerList.addEventListener("input", function (event) {
                    const noteField = event.target.closest("[data-note-input]");
                    if (!noteField) {
                        return;
                    }

                    updateLineNotes(noteField.dataset.noteInput, noteField.value);
                });
            }

            if (!selectedPaymentMethod) {
                const firstPayment = summaryPrompts[0] || drawerPrompts[0];
                if (firstPayment) {
                    selectedPaymentMethod = firstPayment.value;
                }
            }

            syncPaymentMethod(selectedPaymentMethod);
            applyFilter("");
            renderCart();
        }

        bindPaymentSelectors();
        initInteractions();
    }

    document.addEventListener("DOMContentLoaded", function () {
        window.showToast = showToast;
        initMenuPage();
    });
})();
