using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using FacturacionAlemana.Models;

namespace FacturacionAlemana.Services
{
    /// <summary>
    /// Resultado de validación de factura
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public ValidationResult()
        {
            IsValid = true;
        }

        public void AddError(string message)
        {
            Errors.Add(message);
            IsValid = false;
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }
    }

    /// <summary>
    /// Validador de facturas XRechnung 3.0 / EN 16931
    /// </summary>
    public static class InvoiceValidator
    {
        // Códigos de país ISO 3166-1 alpha-2 válidos
        private static readonly HashSet<string> ValidCountryCodes = new()
        {
            "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR", "AS", "AT",
            "AU", "AW", "AX", "AZ", "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI",
            "BJ", "BL", "BM", "BN", "BO", "BQ", "BR", "BS", "BT", "BV", "BW", "BY",
            "BZ", "CA", "CC", "CD", "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN",
            "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM",
            "DO", "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK",
            "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF", "GG", "GH", "GI", "GL",
            "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM",
            "HN", "HR", "HT", "HU", "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR",
            "IS", "IT", "JE", "JM", "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN",
            "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC", "LI", "LK", "LR", "LS",
            "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MF", "MG", "MH", "MK",
            "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW",
            "MX", "MY", "MZ", "NA", "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP",
            "NR", "NU", "NZ", "OM", "PA", "PE", "PF", "PG", "PH", "PK", "PL", "PM",
            "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW",
            "SA", "SB", "SC", "SD", "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM",
            "SN", "SO", "SR", "SS", "ST", "SV", "SX", "SY", "SZ", "TC", "TD", "TF",
            "TG", "TH", "TJ", "TK", "TL", "TM", "TN", "TO", "TR", "TT", "TV", "TW",
            "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI",
            "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW"
        };

        // Mapa de códigos ISO a prefijos de VAT válidos
        private static readonly Dictionary<string, string[]> VatPrefixesByCountry = new()
        {
            { "DE", new[] { "DE" } },  // Alemania
            { "ES", new[] { "ES" } },  // España
            { "FR", new[] { "FR" } },  // Francia
            { "IT", new[] { "IT" } },  // Italia
            { "AT", new[] { "AT" } },  // Austria
            { "BE", new[] { "BE" } },  // Bélgica
            { "NL", new[] { "NL" } },  // Países Bajos
            { "GR", new[] { "EL" } },  // Grecia (usa EL)
            { "PT", new[] { "PT" } },  // Portugal
            { "SE", new[] { "SE" } },  // Suecia
            { "PL", new[] { "PL" } },  // Polonia
            { "CZ", new[] { "CZ" } },  // República Checa
            { "SK", new[] { "SK" } },  // Eslovaquia
            { "HU", new[] { "HU" } },  // Hungría
            { "RO", new[] { "RO" } },  // Rumania
            { "BG", new[] { "BG" } },  // Bulgaria
            { "HR", new[] { "HR" } },  // Croacia
            { "SI", new[] { "SI" } },  // Eslovenia
            { "LT", new[] { "LT" } },  // Lituania
            { "LV", new[] { "LV" } },  // Letonia
            { "EE", new[] { "EE" } },  // Estonia
            { "IE", new[] { "IE" } },  // Irlanda
            { "MT", new[] { "MT" } },  // Malta
            { "CY", new[] { "CY" } },  // Chipre
            { "LU", new[] { "LU" } },  // Luxemburgo
            { "FI", new[] { "FI" } },  // Finlandia
            { "DK", new[] { "DK" } },  // Dinamarca
            { "NO", new[] { "NO" } },  // Noruega
            { "CH", new[] { "CHE" } }, // Suiza
            { "GB", new[] { "GB" } },  // Reino Unido
        };

        /// <summary>
        /// Valida una factura completa
        /// </summary>
        public static ValidationResult ValidateInvoice(Factura factura)
        {
            var result = new ValidationResult();

            // Validaciones de documento
            ValidateInvoiceNumber(factura.IdElement, result);
            ValidateInvoiceDate(factura.IssueDateElement, result);
            ValidateInvoiceType(factura.TypeCodeElement, result);

            // Validaciones del vendedor (BT-27 a BT-37)
            ValidateSellerData(factura, result);

            // Validaciones del comprador (BT-44 a BT-64)
            ValidateBuyerData(factura, result);

            // Validaciones de productos
            ValidateProducts(factura.Productos, result);

            // Validaciones de moneda y totales
            ValidateCurrency(factura.CurrencyID, result);
            ValidateAmounts(factura, result);

            // Validaciones de pago
            ValidatePaymentData(factura, result);

            return result;
        }

        /// <summary>
        /// Valida el número de factura (BT-1)
        /// </summary>
        private static void ValidateInvoiceNumber(string invoiceNumber, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                result.AddError("Número de factura (BT-1) es obligatorio");
                return;
            }

            // Verificar que no sea demasiado largo
            if (invoiceNumber.Length > 50)
            {
                result.AddError("Número de factura no puede exceder 50 caracteres");
            }

            // Advertencia si no sigue el formato sugerido STR-YY-XXXX
            if (!Regex.IsMatch(invoiceNumber, @"^[A-Z]{3}-\d{2}-\d{4}$"))
            {
                result.AddWarning($"Número de factura '{invoiceNumber}' no sigue el formato recomendado STR-YY-XXXX");
            }
        }

        /// <summary>
        /// Valida la fecha de emisión (BT-2)
        /// </summary>
        private static void ValidateInvoiceDate(string issueDate, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(issueDate))
            {
                result.AddError("Fecha de emisión (BT-2) es obligatoria");
                return;
            }

            if (!DateTime.TryParseExact(issueDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                result.AddError($"Fecha de emisión debe estar en formato yyyyMMdd");
                return;
            }

            // Advertencia si la fecha es en el futuro
            if (date > DateTime.Now.AddDays(1))
            {
                result.AddWarning("Fecha de emisión es superior a la fecha actual");
            }
        }

        /// <summary>
        /// Valida el tipo de documento (BT-3)
        /// </summary>
        private static void ValidateInvoiceType(string typeCode, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(typeCode))
            {
                result.AddError("Tipo de documento (BT-3) es obligatorio");
                return;
            }

            if (typeCode != "380" && typeCode != "381" && typeCode != "383" && typeCode != "384")
            {
                result.AddWarning($"Código de tipo '{typeCode}' no es estándar. Use: 380 (Invoice), 381 (Credit Note), 383 (Debit Note), 384 (Correction)");
            }
        }

        /// <summary>
        /// Valida datos del vendedor
        /// </summary>
        private static void ValidateSellerData(Factura factura, ValidationResult result)
        {
            // BT-27: Seller name
            if (string.IsNullOrWhiteSpace(factura.SellerName))
            {
                result.AddError("Nombre del vendedor (BT-27) es obligatorio");
            }

            // BT-31: Seller VAT ID
            if (!string.IsNullOrWhiteSpace(factura.SellerVATID))
            {
                ValidateVatId(factura.SellerVATID, factura.SellerCountryID, "Vendedor", result);
            }
            else
            {
                result.AddWarning("ID de IVA del vendedor (BT-31) es recomendado");
            }

            // BT-32: Seller address - LineOne
            if (string.IsNullOrWhiteSpace(factura.SellerLineOne))
            {
                result.AddWarning("Dirección del vendedor (BT-32) es recomendada");
            }

            // BT-34: Seller postal code
            if (string.IsNullOrWhiteSpace(factura.SellerPostcodeCode))
            {
                result.AddWarning("Código postal del vendedor (BT-34) es recomendado");
            }

            // BT-35: Seller city
            if (string.IsNullOrWhiteSpace(factura.SellerCityName))
            {
                result.AddWarning("Ciudad del vendedor (BT-35) es recomendada");
            }

            // BT-37: Seller country
            if (!string.IsNullOrWhiteSpace(factura.SellerCountryID) && 
                !ValidCountryCodes.Contains(factura.SellerCountryID))
            {
                result.AddError($"Código de país del vendedor (BT-37) '{factura.SellerCountryID}' no es válido. Use ISO 3166-1 alpha-2");
            }
        }

        /// <summary>
        /// Valida datos del comprador
        /// </summary>
        private static void ValidateBuyerData(Factura factura, ValidationResult result)
        {
            // BT-44: Buyer name (OBLIGATORIO)
            if (string.IsNullOrWhiteSpace(factura.BuyerName))
            {
                result.AddError("Nombre del comprador (BT-44) es obligatorio");
            }

            // BT-48: Buyer VAT ID (OBLIGATORIO para EU)
            if (!string.IsNullOrWhiteSpace(factura.BuyerVATID))
            {
                ValidateVatId(factura.BuyerVATID, factura.BuyerCountryID, "Comprador", result);
            }
            else
            {
                result.AddError("ID de IVA del comprador (BT-48) es obligatorio");
            }

            // BT-50: Buyer street address (OBLIGATORIO)
            if (string.IsNullOrWhiteSpace(factura.BuyerLineOne))
            {
                result.AddError("Dirección del comprador (BT-50) es obligatoria");
            }

            // BT-52: Buyer city (OBLIGATORIO)
            if (string.IsNullOrWhiteSpace(factura.BuyerCityName))
            {
                result.AddError("Ciudad del comprador (BT-52) es obligatoria");
            }

            // BT-53: Buyer postal code (OBLIGATORIO)
            if (string.IsNullOrWhiteSpace(factura.BuyerPostcodeCode))
            {
                result.AddError("Código postal del comprador (BT-53) es obligatorio");
            }

            // BT-55: Buyer country (OBLIGATORIO)
            if (string.IsNullOrWhiteSpace(factura.BuyerCountryID))
            {
                result.AddError("País del comprador (BT-55) es obligatorio");
            }
            else if (!ValidCountryCodes.Contains(factura.BuyerCountryID))
            {
                result.AddError($"Código de país del comprador (BT-55) '{factura.BuyerCountryID}' no es válido. Use ISO 3166-1 alpha-2");
            }

            // BT-63: Buyer email (OBLIGATORIO - PEPPOL-EN16931-R010)
            if (string.IsNullOrWhiteSpace(factura.BuyerEmail))
            {
                result.AddError("Email del comprador (BT-63) es obligatorio");
            }
            else if (!IsValidEmail(factura.BuyerEmail))
            {
                result.AddError($"Email del comprador '{factura.BuyerEmail}' no es válido");
            }
        }

        /// <summary>
        /// Valida el ID de IVA
        /// </summary>
        private static void ValidateVatId(string vatId, string countryCode, string party, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(vatId))
                return;

            // Debe tener al menos 3 caracteres (2 de país + 1 de número)
            if (vatId.Length < 3)
            {
                result.AddError($"ID de IVA del {party.ToLower()} '{vatId}' debe tener al menos 3 caracteres");
                return;
            }

            // Los primeros 2 caracteres deben ser letras (código país)
            if (!Regex.IsMatch(vatId.Substring(0, 2), @"^[A-Z]{2}$"))
            {
                result.AddError($"ID de IVA del {party.ToLower()} '{vatId}' debe comenzar con código de país de 2 letras (ej: DE, ES, FR)");
                return;
            }

            string vatPrefix = vatId.Substring(0, 2);

            // Verificar si el prefijo coincide con el país (si está especificado)
            if (!string.IsNullOrWhiteSpace(countryCode) && VatPrefixesByCountry.ContainsKey(countryCode))
            {
                var validPrefixes = VatPrefixesByCountry[countryCode];
                if (!validPrefixes.Contains(vatPrefix))
                {
                    result.AddWarning($"Prefijo de IVA '{vatPrefix}' no corresponde al país '{countryCode}'. Prefijos esperados: {string.Join(", ", validPrefixes)}");
                }
            }

            // Validar formato específico por país (básico)
            if (vatPrefix == "DE" && !Regex.IsMatch(vatId, @"^DE\d{9}$"))
            {
                result.AddWarning($"Formato de IVA alemán inválido: debe ser DExxxxxxxxx (ej: DE123456789)");
            }
            else if (vatPrefix == "ES" && !Regex.IsMatch(vatId, @"^ES[A-Z0-9]{8}[0-9A-Z]$"))
            {
                result.AddWarning($"Formato de IVA español inválido");
            }
            else if (vatPrefix == "FR" && !Regex.IsMatch(vatId, @"^FR[0-9A-Z]{11}$"))
            {
                result.AddWarning($"Formato de IVA francés inválido");
            }
        }

        /// <summary>
        /// Valida productos
        /// </summary>
        private static void ValidateProducts(List<Producto> productos, ValidationResult result)
        {
            if (productos == null || productos.Count == 0)
            {
                result.AddError("Debe haber al menos un producto/línea en la factura");
                return;
            }

            for (int i = 0; i < productos.Count; i++)
            {
                var prod = productos[i];

                // BT-154: Nombre del item
                if (string.IsNullOrWhiteSpace(prod.Name))
                {
                    result.AddError($"Producto {i + 1}: Nombre es obligatorio");
                }

                // BT-129: Cantidad
                if (prod.Cantidad <= 0)
                {
                    result.AddError($"Producto {i + 1}: Cantidad debe ser mayor a 0");
                }

                // BT-131: Precio unitario
                if (prod.PrecioUnitario < 0)
                {
                    result.AddError($"Producto {i + 1}: Precio unitario no puede ser negativo");
                }

                // BT-109: Porcentaje de impuesto
                if (prod.PrecioTotal < 0)
                {
                    result.AddError($"Producto {i + 1}: Precio total no puede ser negativo");
                }
            }
        }

        /// <summary>
        /// Valida la moneda
        /// </summary>
        private static void ValidateCurrency(string currencyCode, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                result.AddError("Código de moneda (BT-5) es obligatorio");
                return;
            }

            // Validar que sea un código ISO 4217 válido (3 letras)
            if (!Regex.IsMatch(currencyCode, @"^[A-Z]{3}$"))
            {
                result.AddError($"Código de moneda '{currencyCode}' no es válido. Use formato ISO 4217 (ej: EUR, USD)");
            }
        }        /// <summary>
        /// Valida los importes
        /// </summary>
        private static void ValidateAmounts(Factura factura, ValidationResult result)
        {
            // Convertir strings a decimales
            if (!decimal.TryParse(factura.BasisAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var basisAmount))
            {
                result.AddError("Importe base inválido");
                return;
            }

            if (!decimal.TryParse(factura.CalculatedAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var calculatedAmount))
            {
                result.AddError("Importe de impuesto inválido");
                return;
            }

            if (!decimal.TryParse(factura.GrandTotalAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var grandTotalAmount))
            {
                result.AddError("Total general inválido");
                return;
            }

            if (!decimal.TryParse(factura.DuePayableAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var duePayableAmount))
            {
                result.AddError("Importe pagadero inválido");
                return;
            }

            if (basisAmount < 0)
            {
                result.AddError("Importe base no puede ser negativo");
            }

            if (calculatedAmount < 0)
            {
                result.AddError("Importe de impuesto no puede ser negativo");
            }

            if (grandTotalAmount < 0)
            {
                result.AddError("Total general no puede ser negativo");
            }

            if (duePayableAmount < 0)
            {
                result.AddError("Importe pagadero no puede ser negativo");
            }            // Verificar que el total sea consistente
            var expectedGrandTotal = basisAmount + calculatedAmount;
            if (Math.Abs(grandTotalAmount - expectedGrandTotal) > 0.01m)
            {
                result.AddError($"Total general (${grandTotalAmount}) no coincide con Importe base (${basisAmount}) + Impuesto (${calculatedAmount}) = ${expectedGrandTotal}");
            }
        }

        /// <summary>
        /// Valida datos de pago
        /// </summary>
        private static void ValidatePaymentData(Factura factura, ValidationResult result)
        {
            // BT-81: Payment means code
            if (string.IsNullOrWhiteSpace(factura.PaymentTypeCode))
            {
                result.AddWarning("Código de medio de pago (BT-81) es recomendado");
            }

            // BT-84: IBAN
            if (!string.IsNullOrWhiteSpace(factura.IBANID))
            {
                ValidateIban(factura.IBANID, result);
            }
            else if (factura.PaymentTypeCode == "30") // 30 = SEPA
            {
                result.AddError("IBAN es obligatorio para pagos SEPA");
            }
        }

        /// <summary>
        /// Valida IBAN
        /// </summary>
        private static void ValidateIban(string iban, ValidationResult result)
        {
            // Remover espacios
            iban = iban.Replace(" ", "");

            // Debe tener entre 15 y 34 caracteres
            if (iban.Length < 15 || iban.Length > 34)
            {
                result.AddError($"IBAN '{iban}' tiene longitud inválida. Debe estar entre 15 y 34 caracteres");
                return;
            }

            // Debe empezar con 2 letras (código país)
            if (!Regex.IsMatch(iban.Substring(0, 2), @"^[A-Z]{2}$"))
            {
                result.AddError($"IBAN '{iban}' debe comenzar con código de país de 2 letras");
                return;
            }

            // Debe tener 2 dígitos de control después del país
            if (!Regex.IsMatch(iban.Substring(2, 2), @"^\d{2}$"))
            {
                result.AddError($"IBAN '{iban}' debe tener 2 dígitos de control después del código de país");
                return;
            }

            // El resto debe ser alfanumérico
            if (!Regex.IsMatch(iban.Substring(4), @"^[A-Z0-9]*$"))
            {
                result.AddError($"IBAN '{iban}' contiene caracteres inválidos");
            }
        }

        /// <summary>
        /// Valida formato de email
        /// </summary>
        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
