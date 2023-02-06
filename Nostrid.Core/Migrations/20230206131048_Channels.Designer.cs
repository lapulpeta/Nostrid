﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nostrid.Model;

#nullable disable

namespace Nostrid.Migrations
{
    [DbContext(typeof(Context))]
    [Migration("20230206131048_Channels")]
    partial class Channels
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.2");

            modelBuilder.Entity("Nostrid.Model.Account", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<DateTimeOffset?>("FollowsLastUpdate")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastNotificationRead")
                        .HasColumnType("TEXT");

                    b.Property<string>("PrivKey")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Id");

                    b.ToTable("Accounts");
                });

            modelBuilder.Entity("Nostrid.Model.AccountDetails", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("About")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "about");

                    b.Property<long>("DetailsLastReceived")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("DetailsLastUpdate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Lud06Url")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "lud06");

                    b.Property<string>("Lud16Id")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "lud16");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<string>("Nip05Id")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "nip05");

                    b.Property<bool>("Nip05Valid")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PictureUrl")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "picture");

                    b.HasKey("Id");

                    b.HasIndex("Id");

                    b.HasIndex("Id", "DetailsLastReceived");

                    b.ToTable("AccountDetails");
                });

            modelBuilder.Entity("Nostrid.Model.Channel", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("CreatorId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Id");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("Nostrid.Model.ChannelDetails", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("About")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "about");

                    b.Property<DateTime>("DetailsLastUpdate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<string>("PictureUrl")
                        .HasColumnType("TEXT")
                        .HasAnnotation("Relational:JsonPropertyName", "picture");

                    b.HasKey("Id");

                    b.HasIndex("Id");

                    b.ToTable("ChannelDetails");
                });

            modelBuilder.Entity("Nostrid.Model.Config", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("MainAccountId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("ManualRelayManagement")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MinDiffIncoming")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("ShowDifficulty")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("StrictDiffCheck")
                        .HasColumnType("INTEGER");

                    b.Property<int>("TargetDiffOutgoing")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Theme")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Configs");
                });

            modelBuilder.Entity("Nostrid.Model.Event", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Broadcast")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("CanEcho")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Content")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<long>("CreatedAtCurated")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Deleted")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Difficulty")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("HasPow")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Kind")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PublicKey")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ReplyToId")
                        .HasColumnType("TEXT");

                    b.Property<string>("ReplyToRootId")
                        .HasColumnType("TEXT");

                    b.Property<string>("RepostEventId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Signature")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAtCurated");

                    b.HasIndex("Id");

                    b.HasIndex("Kind");

                    b.HasIndex("PublicKey");

                    b.HasIndex("Kind", "ChannelId");

                    b.HasIndex("Kind", "ReplyToId");

                    b.HasIndex("Kind", "ReplyToRootId");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("Nostrid.Model.EventSeen", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("EventId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long>("RelayId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("EventId", "RelayId")
                        .IsUnique();

                    b.ToTable("EventSeen");
                });

            modelBuilder.Entity("Nostrid.Model.FeedSource", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Hashtags")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("FeedSources");
                });

            modelBuilder.Entity("Nostrid.Model.Follow", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AccountId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FollowId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("FollowId");

                    b.HasIndex("AccountId", "FollowId");

                    b.ToTable("Follows");
                });

            modelBuilder.Entity("Nostrid.Model.Relay", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Priority")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Read")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Uri")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("Write")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Relays");
                });

            modelBuilder.Entity("Nostrid.Model.TagData", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Data0")
                        .HasColumnType("TEXT");

                    b.Property<string>("Data1")
                        .HasColumnType("TEXT");

                    b.Property<string>("Data2")
                        .HasColumnType("TEXT");

                    b.Property<string>("Data3")
                        .HasColumnType("TEXT");

                    b.Property<int>("DataCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("EventId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("TagIndex")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Data0");

                    b.HasIndex("EventId");

                    b.HasIndex("Data0", "Data1");

                    b.ToTable("TagDatas");
                });

            modelBuilder.Entity("Nostrid.Model.AccountDetails", b =>
                {
                    b.HasOne("Nostrid.Model.Account", "Account")
                        .WithOne("Details")
                        .HasForeignKey("Nostrid.Model.AccountDetails", "Id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Account");
                });

            modelBuilder.Entity("Nostrid.Model.ChannelDetails", b =>
                {
                    b.HasOne("Nostrid.Model.Channel", "Channel")
                        .WithOne("Details")
                        .HasForeignKey("Nostrid.Model.ChannelDetails", "Id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Channel");
                });

            modelBuilder.Entity("Nostrid.Model.TagData", b =>
                {
                    b.HasOne("Nostrid.Model.Event", "Event")
                        .WithMany("Tags")
                        .HasForeignKey("EventId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Event");
                });

            modelBuilder.Entity("Nostrid.Model.Account", b =>
                {
                    b.Navigation("Details");
                });

            modelBuilder.Entity("Nostrid.Model.Channel", b =>
                {
                    b.Navigation("Details");
                });

            modelBuilder.Entity("Nostrid.Model.Event", b =>
                {
                    b.Navigation("Tags");
                });
#pragma warning restore 612, 618
        }
    }
}