using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace EnviadorPagosWPF.Models
{
    public enum ConnectionStateColor { Disconnected, Connecting, Connected, Warning, Error }

    public class PaymentRow : INotifyPropertyChanged
    {
        private bool _selected;

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    OnPropertyChanged(nameof(Selected));
                }
            }
        }

        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public DateTime DocDate { get; set; }
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public string DocCurrency { get; set; } = "MXN";
        public decimal DocTotal { get; set; }
        public decimal DocTotalFC { get; set; }
        public string EmailTo { get; set; } = string.Empty;
        public string StatusEmail { get; set; } = string.Empty;
        public string StatusLog { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public decimal CashSum { get; set; }
        public decimal CheckSum { get; set; }
        public decimal TransferSum { get; set; }

        public string StatusDisplay => (StatusEmail ?? string.Empty).ToUpperInvariant() switch
        {
            "F" => "Enviar",
            "P" => "Pendiente",
            "S" => "Enviado",
            "E" => "Error al enviar",
            _ => StatusEmail ?? string.Empty
        };

        public List<PaymentInvoice> Invoices { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PaymentInvoice
    {
        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public DateTime DocDate { get; set; }
        public string NumAtCard { get; set; } = string.Empty;
        public string DocCurrency { get; set; } = "MXN";
        public decimal DocTotal { get; set; }
        public decimal DocTotalFC { get; set; }
        public decimal SumApplied { get; set; }
    }
}
