using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Features.Cardapio;

public interface ICardapioService
{
    Task<CriacaoItemCardapioResultado> CriarItemCardapioAsync(
        CriarItemCardapioDto dto,
        CancellationToken cancellationToken = default);

    Task<AlteracaoDisponibilidadeResultado> AtualizarDisponibilidadeAsync(
        int itemCardapioId,
        AtualizarDisponibilidadeCardapioDto dto,
        CancellationToken cancellationToken = default);
}

public enum SituacaoCriacaoItemCardapio
{
    Criado,
    Validacao,
    Conflito
}

public sealed record CriacaoItemCardapioResultado(
    SituacaoCriacaoItemCardapio Situacao,
    ItemCardapioRespostaDto? Item = null,
    string? Mensagem = null);

public enum SituacaoAlteracaoDisponibilidade
{
    Alterada,
    ItemNaoEncontrado,
    ConflitoConcorrencia
}

public sealed record AlteracaoDisponibilidadeResultado(
    SituacaoAlteracaoDisponibilidade Situacao,
    string? Mensagem = null);

public class CardapioService : ICardapioService
{
    private const string MensagemItemDuplicado =
        "O produto já está no cardápio dessa data.";

    private const string MensagemConflitoDisponibilidade =
        "Ocorreu um conflito de concorrência ao atualizar a disponibilidade. Tente novamente.";

    private readonly AppDbContext _context;

    public CardapioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CriacaoItemCardapioResultado> CriarItemCardapioAsync(
        CriarItemCardapioDto dto,
        CancellationToken cancellationToken = default)
    {
        var produto = await BuscarProdutoAsync(dto.ProdutoId, cancellationToken);

        var motivoProdutoInvalido = ObterMotivoProdutoInvalido(produto);

        if (motivoProdutoInvalido is not null)
        {
            return new CriacaoItemCardapioResultado(
                SituacaoCriacaoItemCardapio.Validacao,
                Mensagem: motivoProdutoInvalido);
        }

        var produtoValido = produto!;
        var dataCardapio = dto.Data!.Value;

        var produtoJaNoCardapio = await ProdutoJaEstaNoCardapioAsync(
            dataCardapio,
            produtoValido.Id,
            cancellationToken);

        if (produtoJaNoCardapio)
        {
            return new CriacaoItemCardapioResultado(
                SituacaoCriacaoItemCardapio.Conflito,
                Mensagem: MensagemItemDuplicado);
        }

        var item = new ItemCardapio
        {
            Data = dataCardapio,
            ProdutoId = produtoValido.Id
        };

        var gravado = await GravarItemAsync(item, cancellationToken);

        if (!gravado)
        {
            return new CriacaoItemCardapioResultado(
                SituacaoCriacaoItemCardapio.Conflito,
                Mensagem: MensagemItemDuplicado);
        }

        return new CriacaoItemCardapioResultado(
            SituacaoCriacaoItemCardapio.Criado,
            MapearItemParaResposta(item, produtoValido));
    }

    public async Task<AlteracaoDisponibilidadeResultado> AtualizarDisponibilidadeAsync(
        int itemCardapioId,
        AtualizarDisponibilidadeCardapioDto dto,
        CancellationToken cancellationToken = default)
    {
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                var item = await _context.ItensCardapio
                    .FirstOrDefaultAsync(i => i.Id == itemCardapioId, cancellationToken);

                if (item is null)
                {
                    return new AlteracaoDisponibilidadeResultado(
                        SituacaoAlteracaoDisponibilidade.ItemNaoEncontrado);
                }

                item.Disponivel = dto.Disponivel!.Value;

                await _context.SaveChangesAsync(cancellationToken);
                await transacao.CommitAsync(cancellationToken);

                return new AlteracaoDisponibilidadeResultado(
                    SituacaoAlteracaoDisponibilidade.Alterada);
            },
            () => new AlteracaoDisponibilidadeResultado(
                SituacaoAlteracaoDisponibilidade.ConflitoConcorrencia,
                Mensagem: MensagemConflitoDisponibilidade),
            cancellationToken);
    }

    private Task<Produto?> BuscarProdutoAsync(
        int produtoId,
        CancellationToken cancellationToken)
    {
        return _context.Produtos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == produtoId, cancellationToken);
    }

    private static string? ObterMotivoProdutoInvalido(Produto? produto)
    {
        if (produto is null)
        {
            return "O produto informado não existe.";
        }

        if (produto.Tipo != TipoProduto.Marmita)
        {
            return "Somente marmitas podem ser incluídas no cardápio diário.";
        }

        if (!produto.Ativo)
        {
            return "O produto está inativo.";
        }

        return null;
    }

    private Task<bool> ProdutoJaEstaNoCardapioAsync(
        DateOnly dataCardapio,
        int produtoId,
        CancellationToken cancellationToken)
    {
        return _context.ItensCardapio
            .AnyAsync(
                i => i.Data == dataCardapio && i.ProdutoId == produtoId,
                cancellationToken);
    }

    /// <summary>
    /// Grava o item tratando a corrida entre requests: a checagem prévia responde
    /// rápido, mas só o índice único garante que dois pedidos simultâneos não dupliquem.
    /// </summary>
    private async Task<bool> GravarItemAsync(
        ItemCardapio item,
        CancellationToken cancellationToken)
    {
        _context.ItensCardapio.Add(item);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (
            FalhasBanco.EhViolacaoDeIndiceUnico(
                ex,
                IndicesUnicos.ItemCardapioPorDataEProduto))
        {
            return false;
        }
    }

    private static ItemCardapioRespostaDto MapearItemParaResposta(
        ItemCardapio item,
        Produto produto)
    {
        return new ItemCardapioRespostaDto
        {
            Id = item.Id,
            Data = item.Data,
            ProdutoId = item.ProdutoId,
            NomeProduto = produto.Nome,
            Preco = produto.Preco,
            Disponivel = item.Disponivel
        };
    }
}
