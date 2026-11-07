using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Configurations
{
    public class AdminInicialSettings
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;

        public bool PossuiConfiguracao =>
            !string.IsNullOrWhiteSpace(Nome)
            || !string.IsNullOrWhiteSpace(Email)
            || !string.IsNullOrWhiteSpace(Senha);

        public void Validar()
        {
            if (!PossuiConfiguracao)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Nome))
            {
                throw new InvalidOperationException("Configure AdminInicial:Nome para criar o primeiro Admin.");
            }

            if (Nome.Trim().Length > 100)
            {
                throw new InvalidOperationException("AdminInicial:Nome deve conter no máximo 100 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                throw new InvalidOperationException("Configure AdminInicial:Email para criar o primeiro Admin.");
            }

            var email = Email.Trim();

            if (email.Length > 150 || !new EmailAddressAttribute().IsValid(email))
            {
                throw new InvalidOperationException("AdminInicial:Email não é um e-mail válido.");
            }

            if (string.IsNullOrWhiteSpace(Senha))
            {
                throw new InvalidOperationException("Configure AdminInicial:Senha para criar o primeiro Admin.");
            }

            if (Senha.Length < 6)
            {
                throw new InvalidOperationException("AdminInicial:Senha deve ter pelo menos 6 caracteres.");
            }
        }
    }
}
