document.addEventListener("DOMContentLoaded", function () {

    const form = document.getElementById('hashTestForm');
    const overlay = document.getElementById('loadingOverlay');
    const btn = document.getElementById('runBtn');
    const algorithmSelect = document.getElementById('algorithmSelect');
    const roundsInput = document.getElementById('roundsInput');
    const roundsHint = document.getElementById('roundsHint');

    if (!form) return;

    const roundRanges = {
        "Keccak": { min: 1, max: 24 },
        "Blake": { min: 1, max: 14 },
        "Blake2s": { min: 1, max: 10 },
        "Blake2b": { min: 1, max: 12 },
        "Blake3": { min: 1, max: 7 }
    };

    function getValidationSpan(input) {
        const fieldName = input.getAttribute("name");
        return form.querySelector(`[data-valmsg-for="${fieldName}"]`);
    }

    function setInvalid(input, message) {
        input.classList.add("input-validation-error");

        const span = getValidationSpan(input);
        if (span) {
            span.textContent = message;
            span.classList.remove("field-validation-valid");
            span.classList.add("field-validation-error");
        }
    }

    function clearInvalid(input) {
        input.classList.remove("input-validation-error");

        const span = getValidationSpan(input);
        if (span) {
            span.textContent = "";
            span.classList.remove("field-validation-error");
            span.classList.add("field-validation-valid");
        }
    }

    function validatePositive(input) {
        const value = parseInt(input.value);

        if (isNaN(value) || value < 1) {
            setInvalid(input, "Значение должно быть положительным.");
            return false;
        }

        clearInvalid(input);
        return true;
    }

    function validateRounds() {
        const selected = algorithmSelect.value;
        const range = roundRanges[selected];

        if (!range) return true;

        roundsInput.min = range.min;
        roundsInput.max = range.max;

        roundsHint.textContent =
            `Допустимый диапазон раундов: ${range.min}–${range.max}.`;

        const value = parseInt(roundsInput.value);

        if (isNaN(value) || value < range.min || value > range.max) {
            setInvalid(
                roundsInput,
                `Допустимый диапазон для хэш-функции ${algorithmSelect.value}: ${range.min}–${range.max}.`
            );
            return false;
        }

        clearInvalid(roundsInput);
        return true;
    }

    // Проверка при вводе
    form.querySelectorAll("input[type='number']").forEach(input => {
        input.addEventListener("input", function () {
            if (input.id === "roundsInput") {
                validateRounds();
            } else {
                validatePositive(input);
            }
        });
    });

    algorithmSelect.addEventListener("change", function () {
        validateRounds();
    });

    // Перед отправкой
    form.addEventListener("submit", function (e) {

        const validTests = validatePositive(form.querySelector("[name='TestsCount']"));
        const validSize = validatePositive(form.querySelector("[name='InputSizeBytes']"));
        const validRounds = validateRounds();

        if (!validTests || !validSize || !validRounds) {
            e.preventDefault();
            return;
        }

        if (overlay) overlay.classList.remove('d-none');
        if (btn) setTimeout(() => btn.disabled = true, 0);
    });

    validateRounds();
});