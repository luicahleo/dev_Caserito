using CaseritoApp.Identity.Application.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class ValidacionImagenKycTests
{
    private static readonly byte[] _jpegMagic = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] _pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Jpeg_con_magic_bytes_y_content_type_es_valida() =>
        Assert.True(ValidacionImagenKyc.EsImagenValida(_jpegMagic, "image/jpeg"));

    [Fact]
    public void Png_con_magic_bytes_y_content_type_es_valida() =>
        Assert.True(ValidacionImagenKyc.EsImagenValida(_pngMagic, "image/png"));

    [Fact]
    public void Content_type_no_permitido_es_invalida() =>
        Assert.False(ValidacionImagenKyc.EsImagenValida(_jpegMagic, "application/pdf"));

    [Fact]
    public void Magic_bytes_que_no_coinciden_con_content_type_es_invalida() =>
        Assert.False(ValidacionImagenKyc.EsImagenValida(_pngMagic, "image/jpeg"));

    [Fact]
    public void Contenido_vacio_es_invalida() =>
        Assert.False(ValidacionImagenKyc.EsImagenValida([], "image/png"));
}
