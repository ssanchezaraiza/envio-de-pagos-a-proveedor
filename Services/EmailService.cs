using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnviadorPagosWPF.Models;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;

namespace EnviadorPagosWPF.Services
{
    public class EmailService
    {
        private readonly AppConfig _cfg;

        public EmailService(AppConfig cfg)
        {
            _cfg = cfg;
        }

        public async Task<string> SendPaymentEmailAsync(PaymentRow p, List<(string fileName, byte[] data)> attachments)
        {
            var toList = (p.EmailTo ?? string.Empty)
                .Replace(";", ",")
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_cfg.Email.FromName, _cfg.Smtp.User));
            foreach (var to in toList)
            {
                message.To.Add(MailboxAddress.Parse(to));
            }

            var subject = _cfg.Email.SubjectTemplate
                .Replace("{DocNum}", p.DocNum.ToString())
                .Replace("{CardName}", p.CardName);
            message.Subject = subject;

            var builder = new BodyBuilder();
            builder.TextBody = _cfg.Email.BodyTemplate;

            foreach (var att in attachments)
            {
                builder.Attachments.Add(att.fileName, att.data);
            }

            message.Body = builder.ToMessageBody();

            using var transcript = new MemoryStream();
            var logger = new ProtocolLogger(transcript);

            using (var client = new SmtpClient(logger))
            {
                await client.ConnectAsync(_cfg.Smtp.Host, _cfg.Smtp.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
                await client.AuthenticateAsync(_cfg.Smtp.User, _cfg.Smtp.ApiKey);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            transcript.Position = 0;
            string smtpText;
            using (var reader = new StreamReader(transcript, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false))
            {
                smtpText = reader.ReadToEnd();
            }

            var sb = new StringBuilder();
            sb.AppendLine("==== Envio de correo de pago ====");
            sb.AppendLine("DocNum: " + p.DocNum + "  Proveedor: " + p.CardCode + " - " + p.CardName);
            sb.AppendLine("Para: " + string.Join(", ", toList));
            sb.AppendLine("Asunto: " + subject);
            sb.AppendLine("Adjuntos: " + attachments.Count + " -> " + string.Join(", ", attachments.Select(a => a.fileName)));
            sb.AppendLine("=== SMTP Transcript ===");
            sb.AppendLine(smtpText);

            return sb.ToString();
        }
    }
}
