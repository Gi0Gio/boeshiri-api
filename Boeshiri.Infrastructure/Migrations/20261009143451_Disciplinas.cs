using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Disciplinas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "disciplines",
                table: "users",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            // Rellena las familias de quienes ya tienen perfil con lo que escribieron
            // (disciplina libre + etiquetas), con las mismas palabras que usaba el
            // filtro de la Comunidad. Así nadie desaparece del filtro al desplegar; luego
            // cada quien las corrige en Mi perfil. «Otra» o «Rol» no caen en ninguna.
            migrationBuilder.Sql("""
                UPDATE users u
                SET disciplines = ARRAY(
                    SELECT f.clave
                    FROM (VALUES
                        (1, 'dibujo', 'dibujo|ilustracion'),
                        (2, 'pintura', 'pintura|pintor|mural|artista visual'),
                        (3, 'foto', 'fotograf|audiovisual|video|edicion|cine'),
                        (4, 'diseno', 'diseno|grafic'),
                        (5, 'escena', 'canto|music|baile|danza|teatro'),
                        (6, 'escritura', 'escritura|escritor|poesia'),
                        (7, 'artesania', 'ceramica|crochet|bisuteria|textil|artesan'),
                        (8, 'tecnologia', 'desarrollo|programacion|web')
                    ) AS f(orden, clave, patron)
                    WHERE translate(lower(
                            coalesce(u.discipline, '') || ' ' ||
                            coalesce((SELECT string_agg(t.name, ' ')
                                      FROM social_tag_user st JOIN social_tags t ON t.id = st.tags_id
                                      WHERE st.users_id = u.id), '')),
                          'áéíóúüñ', 'aeiouun') ~ f.patron
                    ORDER BY f.orden);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disciplines",
                table: "users");
        }
    }
}
