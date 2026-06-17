using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HarmoniQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Land = table.Column<string>(type: "text", nullable: true),
                    Webseite = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instrumente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instrumente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stuecke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Titel = table.Column<string>(type: "text", nullable: false),
                    Jahr = table.Column<int>(type: "integer", nullable: true),
                    Schwierigkeitsgrad = table.Column<int>(type: "integer", nullable: false),
                    Besetzung = table.Column<string>(type: "text", nullable: true),
                    Beschreibung = table.Column<string>(type: "text", nullable: true),
                    OriginalUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stuecke", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserPasskeys",
                columns: table => new
                {
                    CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserPasskeys", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_AspNetUserPasskeys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Personen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sichtbarkeit = table.Column<int>(type: "integer", nullable: false),
                    Biografie = table.Column<string>(type: "text", nullable: true),
                    BildUrl = table.Column<string>(type: "text", nullable: true),
                    Geburtsjahr = table.Column<int>(type: "integer", nullable: true),
                    BenutzerId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personen_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Richtigstellungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BetrifftTyp = table.Column<int>(type: "integer", nullable: false),
                    BetrifftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    EingereichtVonId = table.Column<string>(type: "text", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Antwort = table.Column<string>(type: "text", nullable: true),
                    AntwortAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Richtigstellungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Richtigstellungen_AspNetUsers_EingereichtVonId",
                        column: x => x.EingereichtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Stimmen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bezeichnung = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stimmen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stimmen_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StueckId = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: true),
                    YouTubeVideoId = table.Column<string>(type: "text", nullable: false),
                    Titel = table.Column<string>(type: "text", nullable: false),
                    AufnahmeDatum = table.Column<DateOnly>(type: "date", nullable: true),
                    Ort = table.Column<string>(type: "text", nullable: true),
                    Anlass = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VorgeschlagenVonId = table.Column<string>(type: "text", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_AspNetUsers_VorgeschlagenVonId",
                        column: x => x.VorgeschlagenVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Videos_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Videos_Stuecke_StueckId",
                        column: x => x.StueckId,
                        principalTable: "Stuecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BandbeitrittAntraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeantragtVonId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntschiedenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandbeitrittAntraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_AspNetUsers_BeantragtVonId",
                        column: x => x.BeantragtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BandbeitrittAntraege_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BandMitgliedschaften",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    VonJahr = table.Column<int>(type: "integer", nullable: true),
                    BisJahr = table.Column<int>(type: "integer", nullable: true),
                    Funktion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandMitgliedschaften", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BandMitgliedschaften_Bands_BandId",
                        column: x => x.BandId,
                        principalTable: "Bands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BandMitgliedschaften_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BandMitgliedschaften_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonAnsprueche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: false),
                    Begruendung = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntschiedenAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonAnsprueche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonAnsprueche_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonAnsprueche_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonInstrumente",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonInstrumente", x => new { x.PersonId, x.InstrumentId });
                    table.ForeignKey(
                        name: "FK_PersonInstrumente_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonInstrumente_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Typ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonLinks_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonRollen",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rolle = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRollen", x => new { x.PersonId, x.Rolle });
                    table.ForeignKey(
                        name: "FK_PersonRollen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StueckBeitraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StueckId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rolle = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StueckBeitraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StueckBeitraege_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StueckBeitraege_Stuecke_StueckId",
                        column: x => x.StueckId,
                        principalTable: "Stuecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bewertungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenutzerId = table.Column<string>(type: "text", nullable: true),
                    AnonymerCookieId = table.Column<string>(type: "text", nullable: true),
                    GesamtEindruck = table.Column<int>(type: "INTEGER", nullable: false),
                    Praezision = table.Column<int>(type: "INTEGER", nullable: false),
                    Musikalitaet = table.Column<int>(type: "INTEGER", nullable: false),
                    AkustischeQualitaet = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoQualitaet = table.Column<int>(type: "INTEGER", nullable: false),
                    Kommentar = table.Column<string>(type: "text", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bewertungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bewertungen_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bewertungen_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoMitwirkungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rolle = table.Column<int>(type: "integer", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StimmeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Anmerkung = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VorgeschlagenVonId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoMitwirkungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_AspNetUsers_VorgeschlagenVonId",
                        column: x => x.VorgeschlagenVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Instrumente_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instrumente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Stimmen_StimmeId",
                        column: x => x.StimmeId,
                        principalTable: "Stimmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VideoMitwirkungen_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserPasskeys_UserId",
                table: "AspNetUserPasskeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_BandId",
                table: "BandbeitrittAntraege",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_BeantragtVonId",
                table: "BandbeitrittAntraege",
                column: "BeantragtVonId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_InstrumentId",
                table: "BandbeitrittAntraege",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_PersonId",
                table: "BandbeitrittAntraege",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_BandbeitrittAntraege_Status",
                table: "BandbeitrittAntraege",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BandMitgliedschaften_BandId_PersonId",
                table: "BandMitgliedschaften",
                columns: new[] { "BandId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_BandMitgliedschaften_InstrumentId",
                table: "BandMitgliedschaften",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BandMitgliedschaften_PersonId",
                table: "BandMitgliedschaften",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Bewertungen_BenutzerId",
                table: "Bewertungen",
                column: "BenutzerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bewertungen_VideoId_AnonymerCookieId",
                table: "Bewertungen",
                columns: new[] { "VideoId", "AnonymerCookieId" },
                unique: true,
                filter: "\"AnonymerCookieId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bewertungen_VideoId_BenutzerId",
                table: "Bewertungen",
                columns: new[] { "VideoId", "BenutzerId" },
                unique: true,
                filter: "\"BenutzerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Instrumente_Name",
                table: "Instrumente",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonAnsprueche_BenutzerId",
                table: "PersonAnsprueche",
                column: "BenutzerId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonAnsprueche_PersonId_BenutzerId_Status",
                table: "PersonAnsprueche",
                columns: new[] { "PersonId", "BenutzerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonAnsprueche_Status",
                table: "PersonAnsprueche",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Personen_BenutzerId",
                table: "Personen",
                column: "BenutzerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonInstrumente_InstrumentId",
                table: "PersonInstrumente",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinks_PersonId",
                table: "PersonLinks",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Richtigstellungen_BetrifftTyp_BetrifftId",
                table: "Richtigstellungen",
                columns: new[] { "BetrifftTyp", "BetrifftId" });

            migrationBuilder.CreateIndex(
                name: "IX_Richtigstellungen_EingereichtVonId",
                table: "Richtigstellungen",
                column: "EingereichtVonId");

            migrationBuilder.CreateIndex(
                name: "IX_Richtigstellungen_Status",
                table: "Richtigstellungen",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Stimmen_InstrumentId_Bezeichnung",
                table: "Stimmen",
                columns: new[] { "InstrumentId", "Bezeichnung" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StueckBeitraege_PersonId",
                table: "StueckBeitraege",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_StueckBeitraege_StueckId_PersonId_Rolle",
                table: "StueckBeitraege",
                columns: new[] { "StueckId", "PersonId", "Rolle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_InstrumentId",
                table: "VideoMitwirkungen",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_PersonId",
                table: "VideoMitwirkungen",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_StimmeId",
                table: "VideoMitwirkungen",
                column: "StimmeId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_VideoId",
                table: "VideoMitwirkungen",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoMitwirkungen_VorgeschlagenVonId",
                table: "VideoMitwirkungen",
                column: "VorgeschlagenVonId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_BandId",
                table: "Videos",
                column: "BandId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_StueckId",
                table: "Videos",
                column: "StueckId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_VorgeschlagenVonId",
                table: "Videos",
                column: "VorgeschlagenVonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserPasskeys");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BandbeitrittAntraege");

            migrationBuilder.DropTable(
                name: "BandMitgliedschaften");

            migrationBuilder.DropTable(
                name: "Bewertungen");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "PersonAnsprueche");

            migrationBuilder.DropTable(
                name: "PersonInstrumente");

            migrationBuilder.DropTable(
                name: "PersonLinks");

            migrationBuilder.DropTable(
                name: "PersonRollen");

            migrationBuilder.DropTable(
                name: "Richtigstellungen");

            migrationBuilder.DropTable(
                name: "StueckBeitraege");

            migrationBuilder.DropTable(
                name: "VideoMitwirkungen");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Personen");

            migrationBuilder.DropTable(
                name: "Stimmen");

            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropTable(
                name: "Instrumente");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Bands");

            migrationBuilder.DropTable(
                name: "Stuecke");
        }
    }
}
