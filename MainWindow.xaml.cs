using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EnviadorPagosWPF.Models;
using EnviadorPagosWPF.Services;

namespace EnviadorPagosWPF
{
    public partial class MainWindow : Window
    {
        private AppConfig Cfg => App.Config;
        private ServiceLayerClient? _sl;
        private PaymentsService? _payments;
        private PdfService _pdf = new();
        private EmailService _mailer;
        private bool _loggedIn = false;
        private string? _userName;
        private string? _password;
        private int _selectedCount = 0;
        private int _sentCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            _mailer = new EmailService(Cfg);
            Grid.CurrentCellChanged += (_, __) => UpdateSelectionStatus();

            CompanyCombo.ItemsSource = Cfg.ServiceLayer.Companies.Select(c => c.CompanyDB).ToList();
            if (CompanyCombo.Items.Count > 0) CompanyCombo.SelectedIndex = 0;
        }

        private void SetStatus(ConnectionStateColor state, string text)
        {
            ConnText.Text = text;
            ConnLed.Fill = state switch
            {
                ConnectionStateColor.Connected => Brushes.LimeGreen,
                ConnectionStateColor.Connecting => Brushes.Gold,
                ConnectionStateColor.Warning => Brushes.Orange,
                ConnectionStateColor.Error => Brushes.Red,
                _ => Brushes.DarkRed
            };
        }

        private void RefreshSendButtonState()
        {
            bool any = (Grid.ItemsSource as System.Collections.IEnumerable)?
                .Cast<object>().OfType<PaymentRow>().Any(r => r.Selected) ?? false;
            BtnEnviar.IsEnabled = any;
        }

        private void UpdateSelectionStatus()
        {
            _selectedCount = (Grid.ItemsSource as System.Collections.IEnumerable)?
                .Cast<object>().OfType<PaymentRow>().Count(r => r.Selected) ?? 0;
            RefreshSendButtonState();
            ConnText.Text = $"Seleccionados: {_selectedCount} | Enviados: {_sentCount}";
        }

        private async System.Threading.Tasks.Task<bool> EnsureLoginAsync(bool force = false)
        {
            if (_loggedIn && _sl != null && !force) return true;

            var companyDb = CompanyCombo.SelectedItem?.ToString() ?? string.Empty;
            var dlg = new LoginDialog { Owner = this, CompanyDb = companyDb };
            if (!string.IsNullOrEmpty(_userName)) dlg.UserName = _userName!;
            var ok = dlg.ShowDialog();
            if (ok != true) return false;
            _userName = dlg.UserName; _password = dlg.Password;

            try
            {
                SetStatus(ConnectionStateColor.Connecting, "Conectando al Service Layer...");
                _sl?.Dispose(); _sl = new ServiceLayerClient(Cfg);
                _payments = new PaymentsService(_sl);
                await _sl.LoginAsync(companyDb, _userName!, _password!);
                _loggedIn = true;
                SetStatus(ConnectionStateColor.Connected, $"Conectado a {companyDb} como {_userName}");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(ConnectionStateColor.Error, $"Error de conexión: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Login Service Layer", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async void BtnConectar_Click(object sender, RoutedEventArgs e)
            => await EnsureLoginAsync(true);

        
private async void BtnBuscar_Click(object sender, RoutedEventArgs e)
{
    if (FromDate.SelectedDate is null || ToDate.SelectedDate is null)
    {
        MessageBox.Show(this, "Selecciona un rango de fechas (Desde / Hasta).", "Rango requerido", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    var from = FromDate.SelectedDate.Value.Date;
    var to   = ToDate.SelectedDate.Value.Date;
    if (from > to)
    {
        MessageBox.Show(this, "La fecha 'Desde' no puede ser mayor que 'Hasta'.", "Rango inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    if (!await EnsureLoginAsync()) return;

    try
    {
        // Reset counters for a new search
        _sentCount = 0;
        _selectedCount = 0;
        ConnText.Text = $"Seleccionados: {_selectedCount} | Enviados: {_sentCount}";

        var rows = await _payments!.GetVendorPaymentsAsync(from, to);

        // Force WPF to refresh binding completely
        Grid.ItemsSource = null;
        Grid.Items.Refresh();

        foreach (var r in rows)
            r.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == nameof(PaymentRow.Selected)) UpdateSelectionStatus();
            };

        Grid.ItemsSource = rows;
        UpdateSelectionStatus();
        SetStatus(ConnectionStateColor.Connected, $"Cargados {rows.Count} pago(s).");
    }
    catch (Exception ex)
    {
        SetStatus(ConnectionStateColor.Error, $"Error al listar pagos: {ex.Message}");
        MessageBox.Show(this, ex.Message, "Error al listar", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

            if (!await EnsureLoginAsync()) return;

            try
            {
                var rows = await _payments!.GetVendorPaymentsAsync(
                    FromDate.SelectedDate.Value, ToDate.SelectedDate.Value);
                // subscribe to Selected changes
                foreach (var r in rows)
                    r.PropertyChanged += (s, ev) =>
                    {
                        if (ev.PropertyName == nameof(PaymentRow.Selected)) UpdateSelectionStatus();
                    };

                Grid.ItemsSource = rows;
                UpdateSelectionStatus();
                SetStatus(ConnectionStateColor.Connected, $"Cargados {rows.Count} pago(s).");
            }
            catch (Exception ex)
            {
                SetStatus(ConnectionStateColor.Error, $"Error al listar pagos: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Error al listar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSelTodo_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.ItemsSource is System.Collections.IEnumerable rows)
            {
                bool markAll = !rows.Cast<object>().OfType<PaymentRow>().Any(r => r.Selected);
                foreach (var r in rows.Cast<object>().OfType<PaymentRow>())
                    r.Selected = markAll;
                Grid.Items.Refresh();
                UpdateSelectionStatus();
            }
        }

        private async void BtnEnviar_Click(object sender, RoutedEventArgs e)
        {
            var rows = Grid.ItemsSource as IEnumerable<PaymentRow>;
            if (rows is null) return;
            if (!await EnsureLoginAsync()) return;

            var selected = rows.Where(r => r.Selected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Selecciona al menos un pago.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var p in selected)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(p.EmailTo))
                        throw new InvalidOperationException("El proveedor no tiene correo en OCRD.U_CORPAG.");

                    var atts = _pdf.PrepareAttachments(p);
                    var smtpLog = await _mailer.SendPaymentEmailAsync(p, atts);
                    var log = $"Enviado el {DateTime.Now:dd/MM/yyyy HH:mm} a {p.EmailTo}\n{smtpLog}";
                    await _payments!.UpdateEmailStatusAsync(p.DocEntry, true, log);
                    p.StatusEmail = "S"; p.StatusLog = log;
                    _sentCount++;
                    SetStatus(ConnectionStateColor.Connected, $"Enviado pago {p.DocNum}");
                    ConnText.Text = $"Seleccionados: {_selectedCount} | Enviados: {_sentCount}";
                }
                catch (Exception ex)
                {
                    var log = $"Error {DateTime.Now:dd/MM/yyyy HH:mm}: {ex.Message}\n(To: {p.EmailTo})";
                    try { await _payments!.UpdateEmailStatusAsync(p.DocEntry, false, log); } catch { }
                    p.StatusEmail = "E"; p.StatusLog = log;
                    SetStatus(ConnectionStateColor.Error, $"Error al enviar {p.DocNum}");
                }
            }

            Grid.Items.Refresh();
            UpdateSelectionStatus();
            MessageBox.Show(this, "Proceso de envío terminado.", "Envío",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override async void OnClosed(EventArgs e)
        {
            try { await _sl?.LogoutAsync()!; } catch { }
            base.OnClosed(e);
        }
    }
}
