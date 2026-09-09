/*
 * Generated: 2026-09-09T14:23:16+02:00
 * Command: python3 ./erm_converter.py --output implementation/model.cs --namespace TasModel --entity_base TasEntity --virtual-base nullable-fks --virtual-base organization=base-and-kind model/black-2026-09-08-1557.excalidraw model/blue-2026-09-08-1558.excalidraw model/gray-2026-09-08-1558.excalidraw model/green-2026-09-07-1652.excalidraw model/orange-2026-09-08-1558.excalidraw model/purple-2026-09-08-1557.excalidraw model/red-2026-09-08-1557.excalidraw
 */
#nullable enable
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace TasModel;

public abstract class TasEntity
{
    public Guid Id { get; set; }
}

public enum OrganizationKind
{
    Club = 1,
    Federation = 2,
}

public partial class Account : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship 1:*
    public ICollection<ResultLog> ResultLogs { get; } = [];
    // relationship 1:*
    public ICollection<CompetitorStatusEvent> CompetitorStatusEvents { get; } = [];
    // relationship 1:*
    public ICollection<CompetitionReportEvent> CompetitionReportEvents { get; } = [];
    // relationship 1:*
    public ICollection<ChangeLog> ChangeLogs { get; } = [];
    // relationship *:*:* to Role, Scope
    public ICollection<AccountRoleScope> AccountRoleScopes { get; } = [];
    // relationship 0..1:0..1
    public Person? Person { get; set; }
    public Guid? PersonId { get; set; }
}

public partial class ChangeLog : TasEntity
{
    // relationship *:1
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
}

public partial class Competition : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:*
    public ICollection<CompetitionType> CompetitionTypes { get; } = [];
    // relationship 0..1:*
    public ICollection<CompetitionReportEvent> CompetitionReportEvents { get; } = [];
    // relationship *:* to StartnoStack
    public ICollection<CompStartnoStack> CompStartnoStacks { get; } = [];
    // relationship 1:1..*
    public ICollection<CompetitionCall> CompetitionCalls { get; } = [];
    // relationship 1:*
    public ICollection<Round> Rounds { get; } = [];
    // relationship *:* to Dance
    public ICollection<CompetitionDance> CompetitionDances { get; } = [];
    // relationship 1:* to Result
    public ICollection<ResultResultContext> ResultResultContexts { get; } = [];
    // relationship *:* to Organization
    public ICollection<ContributesCo> ContributesCos { get; } = [];
    // relationship *:1
    public Organization GovernsFederation { get; set; } = null!;
    public Guid GovernsFederationId { get; set; }
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
    // relationship *:*
    public ICollection<Floor> Floors { get; } = [];
    // relationship 0..1:*
    public ICollection<Timeslot> Timeslots { get; } = [];
    // relationship *:*:* to Role, Account
    public ICollection<AccountRoleScope> AccountRoleScopes { get; } = [];
}

public partial class CompetitionCall : TasEntity
{
    // relationship 1..*:1
    public Competition Competition { get; set; } = null!;
    public Guid CompetitionId { get; set; }
    // relationship 1:*
    public ICollection<Competitor> Competitors { get; } = [];
    // relationship 1:* to Result
    public ICollection<ResultResultContext> ResultResultContexts { get; } = [];
    // relationship *:1
    public CompetitionType CompetitionType { get; set; } = null!;
    public Guid CompetitionTypeId { get; set; }
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
}

public partial class CompetitionReportEvent : TasEntity
{
    // relationship *:1
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
    // relationship *:0..1
    public Competition? Competition { get; set; }
    public Guid? CompetitionId { get; set; }
    // relationship *:0..1
    public Round? Round { get; set; }
    public Guid? RoundId { get; set; }
    // relationship *:0..1
    public Dance? Dance { get; set; }
    public Guid? DanceId { get; set; }
}

public partial class CompetitionType : TasEntity
{
    // relationship *:1..*
    public ICollection<ContestValue> CompetitionDefinitionContestValues { get; } = [];
    // relationship *:*
    public ICollection<Competition> Competitions { get; } = [];
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
    // relationship *:* to MarkingType
    public ICollection<MarkingEligible> MarkingEligibles { get; } = [];
    // relationship *:*
    public ICollection<DslScript> DslScripts { get; } = [];
    // relationship 1:*
    public ICollection<CompetitionCall> CompetitionCalls { get; } = [];
}

public partial class Competitor : TasEntity
{
    // relationship 1:*
    public ICollection<CompetitorStatusEvent> CompetitorStatusEvents { get; } = [];
    // relationship *:1
    public CompetitionCall CompetitionCall { get; set; } = null!;
    public Guid CompetitionCallId { get; set; }
    // relationship 1:*
    public ICollection<Participant> Participants { get; } = [];
    // relationship 1:* to Result
    public ICollection<ResultResultOwner> ResultResultOwners { get; } = [];
    // relationship *:*
    public ICollection<Round> Rounds { get; } = [];
    // relationship *:0..1
    public Startno? Startno { get; set; }
    public Guid? StartnoId { get; set; }
    // relationship 1:* to Evaluation
    public ICollection<EvaluationResultOwner> EvaluationResultOwners { get; } = [];
    // relationship *:1
    public Organization Club { get; set; } = null!;
    public Guid ClubId { get; set; }
    // relationship *:* to Person
    public ICollection<CompetitorMember> CompetitorMembers { get; } = [];
}

public partial class CompetitorStatusEvent : TasEntity
{
    // relationship *:1
    public Competitor Competitor { get; set; } = null!;
    public Guid CompetitorId { get; set; }
    // relationship *:1
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
}

public partial class ContestProperty : TasEntity
{
    // relationship 1:*
    public ICollection<ContestValue> ContestValues { get; } = [];
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
}

public partial class ContestValue : TasEntity
{
    // relationship *:1
    public ContestProperty ContestProperty { get; set; } = null!;
    public Guid ContestPropertyId { get; set; }
    // relationship 1..*:*
    public ICollection<CompetitionType> CompetitionDefinitionCompetitionTypes { get; } = [];
    // relationship 1..*:* to Participant
    public ICollection<ParticipantDefinition> ParticipantDefinitions { get; } = [];
    // relationship 1..*:*
    public ICollection<Dance> DanceDefinitionDances { get; } = [];
}

public partial class Country : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
    // relationship 1:*
    public ICollection<Person> People { get; } = [];
}

public partial class Dance : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:1..*
    public ICollection<ContestValue> DanceDefinitionContestValues { get; } = [];
    // relationship 0..1:*
    public ICollection<CompetitionReportEvent> CompetitionReportEvents { get; } = [];
    // relationship *:* to Competition
    public ICollection<CompetitionDance> CompetitionDances { get; } = [];
    // relationship 1:*
    public ICollection<RoundDance> RoundDances { get; } = [];
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
}

public partial class DslScript : TasEntity
{
    // relationship *:*
    public ICollection<CompetitionType> CompetitionTypes { get; } = [];
    // Attribute: purpose
    // Attribute: round_type_set
}

public partial class Evaluation : TasEntity
{
    // relationship *:1
    public ResultLog ResultLog { get; set; } = null!;
    public Guid ResultLogId { get; set; }
    // relationship *:1
    public Round Round { get; set; } = null!;
    public Guid RoundId { get; set; }
    // relationship *:1 to ResultOwner
    public ICollection<EvaluationResultOwner> EvaluationResultOwners { get; } = [];
}

public partial class Event : TasEntity
{
    // relationship 1:*
    public ICollection<Organization> ClientOrganizations { get; } = [];
    // relationship 1:*
    public ICollection<Competition> Competitions { get; } = [];
    // relationship 1:*
    public ICollection<StartnoStack> StartnoStacks { get; } = [];
    // relationship 1:*
    public ICollection<Dance> Dances { get; } = [];
    // relationship 1:*
    public ICollection<Hall> Halls { get; } = [];
    // relationship 1:*
    public ICollection<Syncpoint> Syncpoints { get; } = [];
    // relationship 1:*
    public ICollection<Person> People { get; } = [];
    // relationship 1:*
    public ICollection<Permission> Permissions { get; } = [];
    // relationship 1:*
    public ICollection<Role> Roles { get; } = [];
    // relationship 1:*
    public ICollection<Country> Countries { get; } = [];
    // relationship 1:*
    public ICollection<Account> Accounts { get; } = [];
    // relationship *:* to Organization
    public ICollection<ContributesEv> ContributesEvs { get; } = [];
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
    // relationship *:*:* to Role, Account
    public ICollection<AccountRoleScope> AccountRoleScopes { get; } = [];
}

public partial class Floor : TasEntity
{
    // relationship *:*
    public ICollection<Timeslot> Timeslots { get; } = [];
    // relationship 1:*
    public ICollection<Teamup> Teamups { get; } = [];
    // relationship *:*
    public ICollection<Round> Rounds { get; } = [];
    // relationship *:*
    public ICollection<Competition> Competitions { get; } = [];
    // relationship 1:*
    public ICollection<JudgeEmployment> JudgeEmployments { get; } = [];
    // relationship *:1
    public Hall Hall { get; set; } = null!;
    public Guid HallId { get; set; }
    // relationship *:*:* to Role, Account
    public ICollection<AccountRoleScope> AccountRoleScopes { get; } = [];
}

public partial class Hall : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship 1:*
    public ICollection<Floor> Floors { get; } = [];
}

public partial class Idspace : TasEntity
{
    // relationship *:* to WithExternal
    public ICollection<MapsTo> MapsTos { get; } = [];
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
}

public partial class JudgeEmployment : TasEntity
{
    // relationship *:*
    public ICollection<JudgingValue> JudgingValues { get; } = [];
    // relationship 1:*
    public ICollection<Mark> Marks { get; } = [];
    // relationship *:1
    public Round Round { get; set; } = null!;
    public Guid RoundId { get; set; }
    // relationship *:1
    public Person Person { get; set; } = null!;
    public Guid PersonId { get; set; }
    // relationship *:1
    public Floor Floor { get; set; } = null!;
    public Guid FloorId { get; set; }
}

public partial class JudgingProperty : TasEntity
{
    // relationship 1:*
    public ICollection<JudgingValue> JudgingValues { get; } = [];
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
}

public partial class JudgingValue : TasEntity
{
    // relationship *:1
    public JudgingProperty JudgingProperty { get; set; } = null!;
    public Guid JudgingPropertyId { get; set; }
    // relationship *:*
    public ICollection<JudgeEmployment> JudgeEmployments { get; } = [];
}

public partial class License : TasEntity
{
    // relationship *:1
    public Person Person { get; set; } = null!;
    public Guid PersonId { get; set; }
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
}

public partial class Mark : TasEntity
{
    // relationship *:1
    public MarkType MarkType { get; set; } = null!;
    public Guid MarkTypeId { get; set; }
    // relationship *:1
    public JudgeEmployment JudgeEmployment { get; set; } = null!;
    public Guid JudgeEmploymentId { get; set; }
    // relationship *:1
    public Participant Participant { get; set; } = null!;
    public Guid ParticipantId { get; set; }
    // relationship *:1
    public RoundDance RoundDance { get; set; } = null!;
    public Guid RoundDanceId { get; set; }
    // Attribute: heat_no
}

public partial class MarkProperty : TasEntity
{
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
    // relationship 1:*
    public ICollection<MarkValue> MarkValues { get; } = [];
}

public partial class MarkType : TasEntity
{
    // relationship *:1..*
    public ICollection<MarkValue> MarkValues { get; } = [];
    // relationship *:1
    public Organization Federation { get; set; } = null!;
    public Guid FederationId { get; set; }
    // relationship 1:*
    public ICollection<Mark> Marks { get; } = [];
    // relationship 1..*:*
    public ICollection<MarkingType> MarkingTypes { get; } = [];
}

public partial class MarkValue : TasEntity
{
    // relationship *:1
    public MarkProperty MarkProperty { get; set; } = null!;
    public Guid MarkPropertyId { get; set; }
    // relationship 1..*:*
    public ICollection<MarkType> MarkTypes { get; } = [];
}

public partial class MarkingType : TasEntity
{
    // relationship *:* to CompetitionType
    public ICollection<MarkingEligible> MarkingEligibles { get; } = [];
    // relationship *:1..*
    public ICollection<MarkType> MarkTypes { get; } = [];
}

public partial class Organization : TasEntity
{
    public OrganizationKind Kind { get; set; }
    // relationship *:1
    public Event ClientEvent { get; set; } = null!;
    public Guid ClientEventId { get; set; }
    // relationship *:*
    public ICollection<Organization> PartOfTargets { get; } = [];
    // relationship *:*
    public ICollection<Organization> PartOfSources { get; } = [];
    // relationship *:* to Event
    public ICollection<ContributesEv> ContributesEvs { get; } = [];
    // relationship 1:*
    public ICollection<ContestProperty> ContestProperties { get; } = [];
    // relationship 1:*
    public ICollection<CompetitionType> CompetitionTypes { get; } = [];
    // relationship 1:*
    public ICollection<MarkProperty> MarkProperties { get; } = [];
    // relationship 1:*
    public ICollection<MarkType> MarkTypes { get; } = [];
    // relationship 1:*
    public ICollection<JudgingProperty> JudgingProperties { get; } = [];
    // relationship *:*:* to Person, Participant
    public ICollection<ParticipantMember> ParticipantMembers { get; } = [];
    // relationship *:* to Competition
    public ICollection<ContributesCo> ContributesCos { get; } = [];
    // relationship 1:*
    public ICollection<Competition> GovernsCompetitions { get; } = [];
    // relationship 1:*
    public ICollection<Dance> Dances { get; } = [];
    // relationship 1:*
    public ICollection<Competitor> Competitors { get; } = [];
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
    // relationship 1:*
    public ICollection<Idspace> Idspaces { get; } = [];
    // relationship *:*:* to Role, Account
    public ICollection<AccountRoleScope> AccountRoleScopes { get; } = [];
    // relationship 1:*
    public ICollection<License> Licenses { get; } = [];
}

public partial class Participant : TasEntity
{
    // relationship *:1..* to ContestValue
    public ICollection<ParticipantDefinition> ParticipantDefinitions { get; } = [];
    // relationship *:*:* to Person, Club
    public ICollection<ParticipantMember> ParticipantMembers { get; } = [];
    // relationship 1:*
    public ICollection<Mark> Marks { get; } = [];
    // relationship *:1
    public Competitor Competitor { get; set; } = null!;
    public Guid CompetitorId { get; set; }
    // relationship 1:* to Result
    public ICollection<ResultResultOwner> ResultResultOwners { get; } = [];
    // relationship *:*
    public ICollection<Teamup> Teamups { get; } = [];
    // relationship 1:* to Evaluation
    public ICollection<EvaluationResultOwner> EvaluationResultOwners { get; } = [];
    // relationship *:0..1
    public Startno? Startno { get; set; }
    public Guid? StartnoId { get; set; }
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
}

public partial class Permission : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:*
    public ICollection<Role> Roles { get; } = [];
}

public partial class Person : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:*:* to Participant, Club
    public ICollection<ParticipantMember> ParticipantMembers { get; } = [];
    // relationship *:*
    public ICollection<Teamup> OnStageTeamups { get; } = [];
    // relationship 1:*
    public ICollection<JudgeEmployment> JudgeEmployments { get; } = [];
    // relationship *:* to Competitor
    public ICollection<CompetitorMember> CompetitorMembers { get; } = [];
    // relationship *:* to Idspace
    public ICollection<MapsTo> MapsTos { get; } = [];
    // relationship *:1
    public Country Country { get; set; } = null!;
    public Guid CountryId { get; set; }
    // relationship 0..1:0..1
    public Account? Account { get; set; }
    // relationship 1:*
    public ICollection<License> Licenses { get; } = [];
}

public partial class Result : TasEntity
{
    // relationship *:1
    public ResultLog ResultLog { get; set; } = null!;
    public Guid ResultLogId { get; set; }
    // relationship *:1 to ResultOwner
    public ICollection<ResultResultOwner> ResultResultOwners { get; } = [];
    // relationship *:1 to ResultContext
    public ICollection<ResultResultContext> ResultResultContexts { get; } = [];
    // relationship *:1 to ResultPhase
    public ICollection<ResultResultPhase> ResultResultPhases { get; } = [];
}

public partial class ResultLog : TasEntity
{
    // relationship *:1
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
    // relationship 1:*
    public ICollection<Result> Results { get; } = [];
    // relationship 1:*
    public ICollection<Evaluation> Evaluations { get; } = [];
}

public partial class Role : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:*:* to Scope, Account
    public ICollection<AccountRoleScope> AccountRoleScopes { get; } = [];
    // relationship *:*
    public ICollection<Permission> Permissions { get; } = [];
}

public partial class Round : TasEntity
{
    // relationship 0..1:*
    public ICollection<CompetitionReportEvent> CompetitionReportEvents { get; } = [];
    // relationship *:1
    public Competition Competition { get; set; } = null!;
    public Guid CompetitionId { get; set; }
    // relationship 1:*
    public ICollection<RoundDance> RoundDances { get; } = [];
    // relationship 1:*
    public ICollection<Evaluation> Evaluations { get; } = [];
    // relationship *:*
    public ICollection<Competitor> Competitors { get; } = [];
    // relationship 1:*
    public ICollection<JudgeEmployment> JudgeEmployments { get; } = [];
    // relationship 1:* to Result
    public ICollection<ResultResultPhase> ResultResultPhases { get; } = [];
    // relationship *:*
    public ICollection<Floor> Floors { get; } = [];
    // relationship 0..1:*
    public ICollection<Timeslot> Timeslots { get; } = [];
}

public partial class RoundDance : TasEntity
{
    // relationship *:1
    public Round Round { get; set; } = null!;
    public Guid RoundId { get; set; }
    // relationship *:1
    public Dance Dance { get; set; } = null!;
    public Guid DanceId { get; set; }
    // relationship 1:*
    public ICollection<Teamup> Teamups { get; } = [];
    // relationship 1:*
    public ICollection<Mark> Marks { get; } = [];
    // relationship 1:* to Result
    public ICollection<ResultResultPhase> ResultResultPhases { get; } = [];
    // Attribute: seq_no
}

public partial class Startno : TasEntity
{
    // relationship *:1
    public StartnoStack StartnoStack { get; set; } = null!;
    public Guid StartnoStackId { get; set; }
    // relationship 0..1:*
    public ICollection<Competitor> Competitors { get; } = [];
    // relationship 0..1:*
    public ICollection<Participant> Participants { get; } = [];
}

public partial class StartnoStack : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship *:* to Competition
    public ICollection<CompStartnoStack> CompStartnoStacks { get; } = [];
    // relationship 1:*
    public ICollection<Startno> Startnos { get; } = [];
}

public partial class Syncpoint : TasEntity
{
    // relationship *:1
    public Event Event { get; set; } = null!;
    public Guid EventId { get; set; }
    // relationship 1:*
    public ICollection<Timeslot> PredSpTimeslots { get; } = [];
    // relationship 1:*
    public ICollection<Timeslot> SuccSpTimeslots { get; } = [];
}

public partial class Teamup : TasEntity
{
    // relationship *:*
    public ICollection<Person> OnStagePeople { get; } = [];
    // relationship *:1
    public RoundDance RoundDance { get; set; } = null!;
    public Guid RoundDanceId { get; set; }
    // relationship *:*
    public ICollection<Participant> Participants { get; } = [];
    // relationship *:1
    public Floor Floor { get; set; } = null!;
    public Guid FloorId { get; set; }
    // Attribute: heat_no
}

public partial class Timeslot : TasEntity
{
    // relationship *:1
    public Syncpoint PredSpSyncpoint { get; set; } = null!;
    public Guid PredSpSyncpointId { get; set; }
    // relationship *:1
    public Syncpoint SuccSpSyncpoint { get; set; } = null!;
    public Guid SuccSpSyncpointId { get; set; }
    // relationship *:*
    public ICollection<Floor> Floors { get; } = [];
    // relationship *:0..1
    public Competition? Competition { get; set; }
    public Guid? CompetitionId { get; set; }
    // relationship *:0..1
    public Round? Round { get; set; }
    public Guid? RoundId { get; set; }
}

public partial class ContributesEv : TasEntity
{
    // relationship *:* to Event
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    // relationship *:* to Organization
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    // Attribute: contribution_type
}

public partial class ParticipantDefinition : TasEntity
{
    // relationship 1..*:* to Participant
    public Guid ContestValueId { get; set; }
    public ContestValue ContestValue { get; set; } = null!;
    // relationship *:1..* to ContestValue
    public Guid ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    // Attribute: conjunction_no
}

public partial class MarkingEligible : TasEntity
{
    // relationship *:* to CompetitionType
    public Guid MarkingTypeId { get; set; }
    public MarkingType MarkingType { get; set; } = null!;
    // relationship *:* to MarkingType
    public Guid CompetitionTypeId { get; set; }
    public CompetitionType CompetitionType { get; set; } = null!;
    // Attribute: round_type
}

public partial class CompStartnoStack : TasEntity
{
    // relationship *:* to Competition
    public Guid StartnoStackId { get; set; }
    public StartnoStack StartnoStack { get; set; } = null!;
    // relationship *:* to StartnoStack
    public Guid CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    // Attribute: purpose
    // Attribute: number range
}

public partial class CompetitionDance : TasEntity
{
    // relationship *:* to Dance
    public Guid CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    // relationship *:* to Competition
    public Guid DanceId { get; set; }
    public Dance Dance { get; set; } = null!;
    // Attribute: seq_no
}

public partial class ParticipantMember : TasEntity
{
    // relationship *:*:* to Participant, Club
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    // relationship *:*:* to Person, Club
    public Guid ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    // relationship *:*:* to Person, Participant
    public Guid ClubId { get; set; }
    public Organization Club { get; set; } = null!;
    // Attribute: role
    // Attribute: seq_no
}

public partial class ResultResultOwner : TasEntity
{
    // relationship 1:* to Result
    public Guid? CompetitorId { get; set; }
    public Competitor? Competitor { get; set; }
    // relationship 1:* to Result
    public Guid? ParticipantId { get; set; }
    public Participant? Participant { get; set; }
    // relationship *:1 to ResultOwner
    public Guid ResultId { get; set; }
    public Result Result { get; set; } = null!;
}

public partial class ResultResultContext : TasEntity
{
    // relationship *:1 to ResultContext
    public Guid ResultId { get; set; }
    public Result Result { get; set; } = null!;
    // relationship 1:* to Result
    public Guid? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    // relationship 1:* to Result
    public Guid? CompetitionCallId { get; set; }
    public CompetitionCall? CompetitionCall { get; set; }
}

public partial class ContributesCo : TasEntity
{
    // relationship *:* to Competition
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    // relationship *:* to Organization
    public Guid CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    // Attribute: contribution_type
}

public partial class ResultResultPhase : TasEntity
{
    // relationship *:1 to ResultPhase
    public Guid ResultId { get; set; }
    public Result Result { get; set; } = null!;
    // relationship 1:* to Result
    public Guid? RoundId { get; set; }
    public Round? Round { get; set; }
    // relationship 1:* to Result
    public Guid? RoundDanceId { get; set; }
    public RoundDance? RoundDance { get; set; }
}

public partial class EvaluationResultOwner : TasEntity
{
    // relationship *:1 to ResultOwner
    public Guid EvaluationId { get; set; }
    public Evaluation Evaluation { get; set; } = null!;
    // relationship 1:* to Evaluation
    public Guid? CompetitorId { get; set; }
    public Competitor? Competitor { get; set; }
    // relationship 1:* to Evaluation
    public Guid? ParticipantId { get; set; }
    public Participant? Participant { get; set; }
}

public partial class CompetitorMember : TasEntity
{
    // relationship *:* to Person
    public Guid CompetitorId { get; set; }
    public Competitor Competitor { get; set; } = null!;
    // relationship *:* to Competitor
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    // Attribute: role
}

public partial class MapsTo : TasEntity
{
    // relationship *:* to WithExternal
    public Guid IdspaceId { get; set; }
    public Idspace Idspace { get; set; } = null!;
    // relationship *:* to Idspace
    public Guid? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    // relationship *:* to Idspace
    public Guid? CompetitionCallId { get; set; }
    public CompetitionCall? CompetitionCall { get; set; }
    // relationship *:* to Idspace
    public Guid? CountryId { get; set; }
    public Country? Country { get; set; }
    // relationship *:* to Idspace
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }
    // relationship *:* to Idspace
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    // relationship *:* to Idspace
    public Guid? ParticipantId { get; set; }
    public Participant? Participant { get; set; }
    // relationship *:* to Idspace
    public Guid? PersonId { get; set; }
    public Person? Person { get; set; }
    // Attribute: external_id
}

public partial class AccountRoleScope : TasEntity
{
    // relationship *:*:* to Scope, Account
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    // relationship *:*:* to Role, Account
    public Guid? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    // relationship *:*:* to Role, Account
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }
    // relationship *:*:* to Role, Account
    public Guid? FloorId { get; set; }
    public Floor? Floor { get; set; }
    // relationship *:*:* to Role, Account
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    // relationship *:*:* to Role, Scope
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
}

public static class ModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>().HasAlternateKey(e => new { e.Id, e.Kind });
        modelBuilder.Entity<Organization>().ToTable(t => t.HasCheckConstraint("CK_Organization_Kind", "\"Kind\" IN (1, 2)"));
        modelBuilder.Entity<Organization>().HasOne(e => e.ClientEvent).WithMany(e => e.ClientOrganizations).HasForeignKey(e => e.ClientEventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Organization>().HasIndex(e => e.ClientEventId);
        modelBuilder.Entity<Organization>()
            .HasMany(e => e.PartOfTargets)
            .WithMany(e => e.PartOfSources)
            .UsingEntity<Dictionary<string, object>>(
                "PartOf",
                right => right.HasOne<Organization>().WithMany().HasForeignKey("TargetOrganizationId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Organization>().WithMany().HasForeignKey("SourceOrganizationId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("SourceOrganizationId", "TargetOrganizationId"));
        modelBuilder.Entity<Competition>().HasOne(e => e.Event).WithMany(e => e.Competitions).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Competition>().HasIndex(e => e.EventId);
        modelBuilder.Entity<StartnoStack>().HasOne(e => e.Event).WithMany(e => e.StartnoStacks).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StartnoStack>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Dance>().HasOne(e => e.Event).WithMany(e => e.Dances).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Dance>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Hall>().HasOne(e => e.Event).WithMany(e => e.Halls).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Hall>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Syncpoint>().HasOne(e => e.Event).WithMany(e => e.Syncpoints).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Syncpoint>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Person>().HasOne(e => e.Event).WithMany(e => e.People).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Person>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Permission>().HasOne(e => e.Event).WithMany(e => e.Permissions).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Permission>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Role>().HasOne(e => e.Event).WithMany(e => e.Roles).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Role>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Country>().HasOne(e => e.Event).WithMany(e => e.Countries).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Country>().HasIndex(e => e.EventId);
        modelBuilder.Entity<Account>().HasOne(e => e.Event).WithMany(e => e.Accounts).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Account>().HasIndex(e => e.EventId);
        modelBuilder.Entity<ContestValue>().HasOne(e => e.ContestProperty).WithMany(e => e.ContestValues).HasForeignKey(e => e.ContestPropertyId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContestValue>().HasIndex(e => e.ContestPropertyId);
        modelBuilder.Entity<ContestProperty>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<ContestProperty>().ToTable(t => t.HasCheckConstraint("CK_ContestProperty_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<ContestProperty>().HasOne(e => e.Federation).WithMany(e => e.ContestProperties).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContestProperty>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<CompetitionType>()
            .HasMany(e => e.CompetitionDefinitionContestValues)
            .WithMany(e => e.CompetitionDefinitionCompetitionTypes)
            .UsingEntity<Dictionary<string, object>>(
                "CompetitionDefinition",
                right => right.HasOne<ContestValue>().WithMany().HasForeignKey("ContestValueId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<CompetitionType>().WithMany().HasForeignKey("CompetitionTypeId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("CompetitionTypeId", "ContestValueId"));
        modelBuilder.Entity<Competition>()
            .HasMany(e => e.CompetitionTypes)
            .WithMany(e => e.Competitions)
            .UsingEntity<Dictionary<string, object>>(
                "CompetitionCompetitionType",
                right => right.HasOne<CompetitionType>().WithMany().HasForeignKey("CompetitionTypeId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Competition>().WithMany().HasForeignKey("CompetitionId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("CompetitionId", "CompetitionTypeId"));
        modelBuilder.Entity<CompetitionType>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<CompetitionType>().ToTable(t => t.HasCheckConstraint("CK_CompetitionType_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<CompetitionType>().HasOne(e => e.Federation).WithMany(e => e.CompetitionTypes).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionType>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<Dance>()
            .HasMany(e => e.DanceDefinitionContestValues)
            .WithMany(e => e.DanceDefinitionDances)
            .UsingEntity<Dictionary<string, object>>(
                "DanceDefinition",
                right => right.HasOne<ContestValue>().WithMany().HasForeignKey("ContestValueId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Dance>().WithMany().HasForeignKey("DanceId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("DanceId", "ContestValueId"));
        modelBuilder.Entity<DslScript>()
            .HasMany(e => e.CompetitionTypes)
            .WithMany(e => e.DslScripts)
            .UsingEntity<Dictionary<string, object>>(
                "CompetitionTypeDslScript",
                right => right.HasOne<CompetitionType>().WithMany().HasForeignKey("CompetitionTypeId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<DslScript>().WithMany().HasForeignKey("DslScriptId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("DslScriptId", "CompetitionTypeId"));
        modelBuilder.Entity<MarkProperty>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<MarkProperty>().ToTable(t => t.HasCheckConstraint("CK_MarkProperty_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<MarkProperty>().HasOne(e => e.Federation).WithMany(e => e.MarkProperties).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarkProperty>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<MarkValue>().HasOne(e => e.MarkProperty).WithMany(e => e.MarkValues).HasForeignKey(e => e.MarkPropertyId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarkValue>().HasIndex(e => e.MarkPropertyId);
        modelBuilder.Entity<JudgingValue>().HasOne(e => e.JudgingProperty).WithMany(e => e.JudgingValues).HasForeignKey(e => e.JudgingPropertyId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JudgingValue>().HasIndex(e => e.JudgingPropertyId);
        modelBuilder.Entity<MarkType>()
            .HasMany(e => e.MarkValues)
            .WithMany(e => e.MarkTypes)
            .UsingEntity<Dictionary<string, object>>(
                "MarkTypeMarkValue",
                right => right.HasOne<MarkValue>().WithMany().HasForeignKey("MarkValueId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<MarkType>().WithMany().HasForeignKey("MarkTypeId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("MarkTypeId", "MarkValueId"));
        modelBuilder.Entity<MarkType>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<MarkType>().ToTable(t => t.HasCheckConstraint("CK_MarkType_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<MarkType>().HasOne(e => e.Federation).WithMany(e => e.MarkTypes).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarkType>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<Mark>().HasOne(e => e.MarkType).WithMany(e => e.Marks).HasForeignKey(e => e.MarkTypeId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Mark>().HasIndex(e => e.MarkTypeId);
        modelBuilder.Entity<MarkType>()
            .HasMany(e => e.MarkingTypes)
            .WithMany(e => e.MarkTypes)
            .UsingEntity<Dictionary<string, object>>(
                "MarkTypeMarkingType",
                right => right.HasOne<MarkingType>().WithMany().HasForeignKey("MarkingTypeId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<MarkType>().WithMany().HasForeignKey("MarkTypeId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("MarkTypeId", "MarkingTypeId"));
        modelBuilder.Entity<JudgingProperty>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<JudgingProperty>().ToTable(t => t.HasCheckConstraint("CK_JudgingProperty_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<JudgingProperty>().HasOne(e => e.Federation).WithMany(e => e.JudgingProperties).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JudgingProperty>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<JudgeEmployment>()
            .HasMany(e => e.JudgingValues)
            .WithMany(e => e.JudgeEmployments)
            .UsingEntity<Dictionary<string, object>>(
                "JudgeEmploymentJudgingValue",
                right => right.HasOne<JudgingValue>().WithMany().HasForeignKey("JudgingValueId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<JudgeEmployment>().WithMany().HasForeignKey("JudgeEmploymentId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("JudgeEmploymentId", "JudgingValueId"));
        modelBuilder.Entity<ResultLog>().HasOne(e => e.Account).WithMany(e => e.ResultLogs).HasForeignKey(e => e.AccountId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultLog>().HasIndex(e => e.AccountId);
        modelBuilder.Entity<Result>().HasOne(e => e.ResultLog).WithMany(e => e.Results).HasForeignKey(e => e.ResultLogId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Result>().HasIndex(e => e.ResultLogId);
        modelBuilder.Entity<Evaluation>().HasOne(e => e.ResultLog).WithMany(e => e.Evaluations).HasForeignKey(e => e.ResultLogId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Evaluation>().HasIndex(e => e.ResultLogId);
        modelBuilder.Entity<CompetitorStatusEvent>().HasOne(e => e.Competitor).WithMany(e => e.CompetitorStatusEvents).HasForeignKey(e => e.CompetitorId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitorStatusEvent>().HasIndex(e => e.CompetitorId);
        modelBuilder.Entity<CompetitorStatusEvent>().HasOne(e => e.Account).WithMany(e => e.CompetitorStatusEvents).HasForeignKey(e => e.AccountId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitorStatusEvent>().HasIndex(e => e.AccountId);
        modelBuilder.Entity<CompetitionReportEvent>().HasOne(e => e.Account).WithMany(e => e.CompetitionReportEvents).HasForeignKey(e => e.AccountId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionReportEvent>().HasIndex(e => e.AccountId);
        modelBuilder.Entity<ChangeLog>().HasOne(e => e.Account).WithMany(e => e.ChangeLogs).HasForeignKey(e => e.AccountId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChangeLog>().HasIndex(e => e.AccountId);
        modelBuilder.Entity<CompetitionReportEvent>().HasOne(e => e.Competition).WithMany(e => e.CompetitionReportEvents).HasForeignKey(e => e.CompetitionId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionReportEvent>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<CompetitionReportEvent>().HasOne(e => e.Round).WithMany(e => e.CompetitionReportEvents).HasForeignKey(e => e.RoundId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionReportEvent>().HasIndex(e => e.RoundId);
        modelBuilder.Entity<CompetitionReportEvent>().HasOne(e => e.Dance).WithMany(e => e.CompetitionReportEvents).HasForeignKey(e => e.DanceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionReportEvent>().HasIndex(e => e.DanceId);
        modelBuilder.Entity<Startno>().HasOne(e => e.StartnoStack).WithMany(e => e.Startnos).HasForeignKey(e => e.StartnoStackId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Startno>().HasIndex(e => e.StartnoStackId);
        modelBuilder.Entity<CompetitionCall>().HasOne(e => e.Competition).WithMany(e => e.CompetitionCalls).HasForeignKey(e => e.CompetitionId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionCall>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<Competitor>().HasOne(e => e.CompetitionCall).WithMany(e => e.Competitors).HasForeignKey(e => e.CompetitionCallId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Competitor>().HasIndex(e => e.CompetitionCallId);
        modelBuilder.Entity<Round>().HasOne(e => e.Competition).WithMany(e => e.Rounds).HasForeignKey(e => e.CompetitionId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Round>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<RoundDance>().HasOne(e => e.Round).WithMany(e => e.RoundDances).HasForeignKey(e => e.RoundId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RoundDance>().HasIndex(e => e.RoundId);
        modelBuilder.Entity<Teamup>()
            .HasMany(e => e.OnStagePeople)
            .WithMany(e => e.OnStageTeamups)
            .UsingEntity<Dictionary<string, object>>(
                "OnStage",
                right => right.HasOne<Person>().WithMany().HasForeignKey("PersonId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Teamup>().WithMany().HasForeignKey("TeamupId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("TeamupId", "PersonId"));
        modelBuilder.Entity<RoundDance>().HasOne(e => e.Dance).WithMany(e => e.RoundDances).HasForeignKey(e => e.DanceId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RoundDance>().HasIndex(e => e.DanceId);
        modelBuilder.Entity<Mark>().HasOne(e => e.JudgeEmployment).WithMany(e => e.Marks).HasForeignKey(e => e.JudgeEmploymentId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Mark>().HasIndex(e => e.JudgeEmploymentId);
        modelBuilder.Entity<Teamup>().HasOne(e => e.RoundDance).WithMany(e => e.Teamups).HasForeignKey(e => e.RoundDanceId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Teamup>().HasIndex(e => e.RoundDanceId);
        modelBuilder.Entity<Mark>().HasOne(e => e.Participant).WithMany(e => e.Marks).HasForeignKey(e => e.ParticipantId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Mark>().HasIndex(e => e.ParticipantId);
        modelBuilder.Entity<Participant>().HasOne(e => e.Competitor).WithMany(e => e.Participants).HasForeignKey(e => e.CompetitorId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Participant>().HasIndex(e => e.CompetitorId);
        modelBuilder.Entity<Evaluation>().HasOne(e => e.Round).WithMany(e => e.Evaluations).HasForeignKey(e => e.RoundId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Evaluation>().HasIndex(e => e.RoundId);
        modelBuilder.Entity<Round>()
            .HasMany(e => e.Competitors)
            .WithMany(e => e.Rounds)
            .UsingEntity<Dictionary<string, object>>(
                "CompetitorRound",
                right => right.HasOne<Competitor>().WithMany().HasForeignKey("CompetitorId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Round>().WithMany().HasForeignKey("RoundId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("RoundId", "CompetitorId"));
        modelBuilder.Entity<CompetitionCall>().HasOne(e => e.CompetitionType).WithMany(e => e.CompetitionCalls).HasForeignKey(e => e.CompetitionTypeId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionCall>().HasIndex(e => e.CompetitionTypeId);
        modelBuilder.Entity<Competitor>().HasOne(e => e.Startno).WithMany(e => e.Competitors).HasForeignKey(e => e.StartnoId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Competitor>().HasIndex(e => e.StartnoId);
        modelBuilder.Entity<JudgeEmployment>().HasOne(e => e.Round).WithMany(e => e.JudgeEmployments).HasForeignKey(e => e.RoundId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JudgeEmployment>().HasIndex(e => e.RoundId);
        modelBuilder.Entity<Mark>().HasOne(e => e.RoundDance).WithMany(e => e.Marks).HasForeignKey(e => e.RoundDanceId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Mark>().HasIndex(e => e.RoundDanceId);
        modelBuilder.Entity<JudgeEmployment>().HasOne(e => e.Person).WithMany(e => e.JudgeEmployments).HasForeignKey(e => e.PersonId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JudgeEmployment>().HasIndex(e => e.PersonId);
        modelBuilder.Entity<Competition>().Property<OrganizationKind>("GovernsFederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<Competition>().ToTable(t => t.HasCheckConstraint("CK_Competition_GovernsFederationKind", "\"GovernsFederationKind\" = 2"));
        modelBuilder.Entity<Competition>().HasOne(e => e.GovernsFederation).WithMany(e => e.GovernsCompetitions).HasForeignKey("GovernsFederationId", "GovernsFederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Competition>().HasIndex("GovernsFederationId", "GovernsFederationKind");
        modelBuilder.Entity<Dance>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<Dance>().ToTable(t => t.HasCheckConstraint("CK_Dance_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<Dance>().HasOne(e => e.Federation).WithMany(e => e.Dances).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Dance>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<Participant>()
            .HasMany(e => e.Teamups)
            .WithMany(e => e.Participants)
            .UsingEntity<Dictionary<string, object>>(
                "ParticipantTeamup",
                right => right.HasOne<Teamup>().WithMany().HasForeignKey("TeamupId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Participant>().WithMany().HasForeignKey("ParticipantId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("ParticipantId", "TeamupId"));
        modelBuilder.Entity<Competitor>().Property<OrganizationKind>("ClubKind").HasDefaultValue(OrganizationKind.Club);
        modelBuilder.Entity<Competitor>().ToTable(t => t.HasCheckConstraint("CK_Competitor_ClubKind", "\"ClubKind\" = 1"));
        modelBuilder.Entity<Competitor>().HasOne(e => e.Club).WithMany(e => e.Competitors).HasForeignKey("ClubId", "ClubKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Competitor>().HasIndex("ClubId", "ClubKind");
        modelBuilder.Entity<Participant>().HasOne(e => e.Startno).WithMany(e => e.Participants).HasForeignKey(e => e.StartnoId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Participant>().HasIndex(e => e.StartnoId);
        modelBuilder.Entity<Idspace>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<Idspace>().ToTable(t => t.HasCheckConstraint("CK_Idspace_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<Idspace>().HasOne(e => e.Federation).WithMany(e => e.Idspaces).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Idspace>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<Timeslot>().HasOne(e => e.PredSpSyncpoint).WithMany(e => e.PredSpTimeslots).HasForeignKey(e => e.PredSpSyncpointId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Timeslot>().HasIndex(e => e.PredSpSyncpointId);
        modelBuilder.Entity<Timeslot>().HasOne(e => e.SuccSpSyncpoint).WithMany(e => e.SuccSpTimeslots).HasForeignKey(e => e.SuccSpSyncpointId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Timeslot>().HasIndex(e => e.SuccSpSyncpointId);
        modelBuilder.Entity<Timeslot>()
            .HasMany(e => e.Floors)
            .WithMany(e => e.Timeslots)
            .UsingEntity<Dictionary<string, object>>(
                "FloorTimeslot",
                right => right.HasOne<Floor>().WithMany().HasForeignKey("FloorId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Timeslot>().WithMany().HasForeignKey("TimeslotId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("TimeslotId", "FloorId"));
        modelBuilder.Entity<Teamup>().HasOne(e => e.Floor).WithMany(e => e.Teamups).HasForeignKey(e => e.FloorId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Teamup>().HasIndex(e => e.FloorId);
        modelBuilder.Entity<Round>()
            .HasMany(e => e.Floors)
            .WithMany(e => e.Rounds)
            .UsingEntity<Dictionary<string, object>>(
                "FloorRound",
                right => right.HasOne<Floor>().WithMany().HasForeignKey("FloorId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Round>().WithMany().HasForeignKey("RoundId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("RoundId", "FloorId"));
        modelBuilder.Entity<Competition>()
            .HasMany(e => e.Floors)
            .WithMany(e => e.Competitions)
            .UsingEntity<Dictionary<string, object>>(
                "CompetitionFloor",
                right => right.HasOne<Floor>().WithMany().HasForeignKey("FloorId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Competition>().WithMany().HasForeignKey("CompetitionId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("CompetitionId", "FloorId"));
        modelBuilder.Entity<JudgeEmployment>().HasOne(e => e.Floor).WithMany(e => e.JudgeEmployments).HasForeignKey(e => e.FloorId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JudgeEmployment>().HasIndex(e => e.FloorId);
        modelBuilder.Entity<Floor>().HasOne(e => e.Hall).WithMany(e => e.Floors).HasForeignKey(e => e.HallId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Floor>().HasIndex(e => e.HallId);
        modelBuilder.Entity<Timeslot>().HasOne(e => e.Competition).WithMany(e => e.Timeslots).HasForeignKey(e => e.CompetitionId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Timeslot>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<Timeslot>().HasOne(e => e.Round).WithMany(e => e.Timeslots).HasForeignKey(e => e.RoundId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Timeslot>().HasIndex(e => e.RoundId);
        modelBuilder.Entity<Person>().HasOne(e => e.Country).WithMany(e => e.People).HasForeignKey(e => e.CountryId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Person>().HasIndex(e => e.CountryId);
        modelBuilder.Entity<Account>().HasOne(e => e.Person).WithOne(e => e.Account).HasForeignKey<Account>(e => e.PersonId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Account>().HasIndex(e => e.PersonId).IsUnique();
        modelBuilder.Entity<Permission>()
            .HasMany(e => e.Roles)
            .WithMany(e => e.Permissions)
            .UsingEntity<Dictionary<string, object>>(
                "PermissionRole",
                right => right.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Permission>().WithMany().HasForeignKey("PermissionId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasKey("PermissionId", "RoleId"));
        modelBuilder.Entity<License>().HasOne(e => e.Person).WithMany(e => e.Licenses).HasForeignKey(e => e.PersonId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<License>().HasIndex(e => e.PersonId);
        modelBuilder.Entity<License>().Property<OrganizationKind>("FederationKind").HasDefaultValue(OrganizationKind.Federation);
        modelBuilder.Entity<License>().ToTable(t => t.HasCheckConstraint("CK_License_FederationKind", "\"FederationKind\" = 2"));
        modelBuilder.Entity<License>().HasOne(e => e.Federation).WithMany(e => e.Licenses).HasForeignKey("FederationId", "FederationKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<License>().HasIndex("FederationId", "FederationKind");
        modelBuilder.Entity<ContributesEv>().HasOne(e => e.Organization).WithMany(e => e.ContributesEvs).HasForeignKey(e => e.OrganizationId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributesEv>().HasIndex(e => e.OrganizationId);
        modelBuilder.Entity<ContributesEv>().HasOne(e => e.Event).WithMany(e => e.ContributesEvs).HasForeignKey(e => e.EventId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributesEv>().HasIndex(e => e.EventId);
        modelBuilder.Entity<ContributesEv>().HasIndex(e => new { e.OrganizationId, e.EventId }).IsUnique();
        modelBuilder.Entity<ParticipantDefinition>().HasOne(e => e.ContestValue).WithMany(e => e.ParticipantDefinitions).HasForeignKey(e => e.ContestValueId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParticipantDefinition>().HasIndex(e => e.ContestValueId);
        modelBuilder.Entity<ParticipantDefinition>().HasOne(e => e.Participant).WithMany(e => e.ParticipantDefinitions).HasForeignKey(e => e.ParticipantId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParticipantDefinition>().HasIndex(e => e.ParticipantId);
        modelBuilder.Entity<ParticipantDefinition>().HasIndex(e => new { e.ContestValueId, e.ParticipantId }).IsUnique();
        modelBuilder.Entity<MarkingEligible>().HasOne(e => e.MarkingType).WithMany(e => e.MarkingEligibles).HasForeignKey(e => e.MarkingTypeId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarkingEligible>().HasIndex(e => e.MarkingTypeId);
        modelBuilder.Entity<MarkingEligible>().HasOne(e => e.CompetitionType).WithMany(e => e.MarkingEligibles).HasForeignKey(e => e.CompetitionTypeId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarkingEligible>().HasIndex(e => e.CompetitionTypeId);
        modelBuilder.Entity<MarkingEligible>().HasIndex(e => new { e.MarkingTypeId, e.CompetitionTypeId }).IsUnique();
        modelBuilder.Entity<CompStartnoStack>().HasOne(e => e.StartnoStack).WithMany(e => e.CompStartnoStacks).HasForeignKey(e => e.StartnoStackId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompStartnoStack>().HasIndex(e => e.StartnoStackId);
        modelBuilder.Entity<CompStartnoStack>().HasOne(e => e.Competition).WithMany(e => e.CompStartnoStacks).HasForeignKey(e => e.CompetitionId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompStartnoStack>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<CompStartnoStack>().HasIndex(e => new { e.StartnoStackId, e.CompetitionId }).IsUnique();
        modelBuilder.Entity<CompetitionDance>().HasOne(e => e.Competition).WithMany(e => e.CompetitionDances).HasForeignKey(e => e.CompetitionId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionDance>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<CompetitionDance>().HasOne(e => e.Dance).WithMany(e => e.CompetitionDances).HasForeignKey(e => e.DanceId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitionDance>().HasIndex(e => e.DanceId);
        modelBuilder.Entity<CompetitionDance>().HasIndex(e => new { e.CompetitionId, e.DanceId }).IsUnique();
        modelBuilder.Entity<ParticipantMember>().Property<OrganizationKind>("ClubKind").HasDefaultValue(OrganizationKind.Club);
        modelBuilder.Entity<ParticipantMember>().HasOne(e => e.Person).WithMany(e => e.ParticipantMembers).HasForeignKey(e => e.PersonId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParticipantMember>().HasIndex(e => e.PersonId);
        modelBuilder.Entity<ParticipantMember>().HasOne(e => e.Participant).WithMany(e => e.ParticipantMembers).HasForeignKey(e => e.ParticipantId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParticipantMember>().HasIndex(e => e.ParticipantId);
        modelBuilder.Entity<ParticipantMember>().HasOne(e => e.Club).WithMany(e => e.ParticipantMembers).HasForeignKey("ClubId", "ClubKind").HasPrincipalKey(e => new { e.Id, e.Kind }).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParticipantMember>().HasIndex("ClubId", "ClubKind");
        modelBuilder.Entity<ParticipantMember>().ToTable(t => t.HasCheckConstraint("CK_ParticipantMember_ClubKind", "\"ClubKind\" = 1"));
        modelBuilder.Entity<ParticipantMember>().HasIndex("PersonId", "ParticipantId", "ClubId", "ClubKind").IsUnique();
        modelBuilder.Entity<ResultResultOwner>().HasOne(e => e.Competitor).WithMany(e => e.ResultResultOwners).HasForeignKey(e => e.CompetitorId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultOwner>().HasIndex(e => e.CompetitorId);
        modelBuilder.Entity<ResultResultOwner>().HasOne(e => e.Participant).WithMany(e => e.ResultResultOwners).HasForeignKey(e => e.ParticipantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultOwner>().HasIndex(e => e.ParticipantId);
        modelBuilder.Entity<ResultResultOwner>().HasOne(e => e.Result).WithMany(e => e.ResultResultOwners).HasForeignKey(e => e.ResultId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultOwner>().HasIndex(e => e.ResultId);
        modelBuilder.Entity<ResultResultOwner>().ToTable(t => t.HasCheckConstraint("CK_ResultResultOwner_ResultOwner_ExactlyOne", "CASE WHEN \"CompetitorId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"ParticipantId\" IS NULL THEN 0 ELSE 1 END = 1"));
        modelBuilder.Entity<ResultResultOwner>().HasIndex(e => new { e.CompetitorId, e.ResultId }).IsUnique().HasFilter("\"CompetitorId\" IS NOT NULL");
        modelBuilder.Entity<ResultResultOwner>().HasIndex(e => new { e.ParticipantId, e.ResultId }).IsUnique().HasFilter("\"ParticipantId\" IS NOT NULL");
        modelBuilder.Entity<ResultResultOwner>().HasIndex(e => e.ResultId).IsUnique();
        modelBuilder.Entity<ResultResultContext>().HasOne(e => e.Result).WithMany(e => e.ResultResultContexts).HasForeignKey(e => e.ResultId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultContext>().HasIndex(e => e.ResultId);
        modelBuilder.Entity<ResultResultContext>().HasOne(e => e.Competition).WithMany(e => e.ResultResultContexts).HasForeignKey(e => e.CompetitionId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultContext>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<ResultResultContext>().HasOne(e => e.CompetitionCall).WithMany(e => e.ResultResultContexts).HasForeignKey(e => e.CompetitionCallId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultContext>().HasIndex(e => e.CompetitionCallId);
        modelBuilder.Entity<ResultResultContext>().ToTable(t => t.HasCheckConstraint("CK_ResultResultContext_ResultContext_ExactlyOne", "CASE WHEN \"CompetitionId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CompetitionCallId\" IS NULL THEN 0 ELSE 1 END = 1"));
        modelBuilder.Entity<ResultResultContext>().HasIndex(e => e.ResultId).IsUnique();
        modelBuilder.Entity<ResultResultContext>().HasIndex(e => new { e.ResultId, e.CompetitionCallId }).IsUnique().HasFilter("\"CompetitionCallId\" IS NOT NULL");
        modelBuilder.Entity<ResultResultContext>().HasIndex(e => new { e.ResultId, e.CompetitionId }).IsUnique().HasFilter("\"CompetitionId\" IS NOT NULL");
        modelBuilder.Entity<ContributesCo>().HasOne(e => e.Organization).WithMany(e => e.ContributesCos).HasForeignKey(e => e.OrganizationId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributesCo>().HasIndex(e => e.OrganizationId);
        modelBuilder.Entity<ContributesCo>().HasOne(e => e.Competition).WithMany(e => e.ContributesCos).HasForeignKey(e => e.CompetitionId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributesCo>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<ContributesCo>().HasIndex(e => new { e.OrganizationId, e.CompetitionId }).IsUnique();
        modelBuilder.Entity<ResultResultPhase>().HasOne(e => e.Result).WithMany(e => e.ResultResultPhases).HasForeignKey(e => e.ResultId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultPhase>().HasIndex(e => e.ResultId);
        modelBuilder.Entity<ResultResultPhase>().HasOne(e => e.Round).WithMany(e => e.ResultResultPhases).HasForeignKey(e => e.RoundId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultPhase>().HasIndex(e => e.RoundId);
        modelBuilder.Entity<ResultResultPhase>().HasOne(e => e.RoundDance).WithMany(e => e.ResultResultPhases).HasForeignKey(e => e.RoundDanceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResultResultPhase>().HasIndex(e => e.RoundDanceId);
        modelBuilder.Entity<ResultResultPhase>().ToTable(t => t.HasCheckConstraint("CK_ResultResultPhase_ResultPhase_ExactlyOne", "CASE WHEN \"RoundId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"RoundDanceId\" IS NULL THEN 0 ELSE 1 END = 1"));
        modelBuilder.Entity<ResultResultPhase>().HasIndex(e => e.ResultId).IsUnique();
        modelBuilder.Entity<ResultResultPhase>().HasIndex(e => new { e.ResultId, e.RoundDanceId }).IsUnique().HasFilter("\"RoundDanceId\" IS NOT NULL");
        modelBuilder.Entity<ResultResultPhase>().HasIndex(e => new { e.ResultId, e.RoundId }).IsUnique().HasFilter("\"RoundId\" IS NOT NULL");
        modelBuilder.Entity<EvaluationResultOwner>().HasOne(e => e.Evaluation).WithMany(e => e.EvaluationResultOwners).HasForeignKey(e => e.EvaluationId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EvaluationResultOwner>().HasIndex(e => e.EvaluationId);
        modelBuilder.Entity<EvaluationResultOwner>().HasOne(e => e.Competitor).WithMany(e => e.EvaluationResultOwners).HasForeignKey(e => e.CompetitorId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EvaluationResultOwner>().HasIndex(e => e.CompetitorId);
        modelBuilder.Entity<EvaluationResultOwner>().HasOne(e => e.Participant).WithMany(e => e.EvaluationResultOwners).HasForeignKey(e => e.ParticipantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EvaluationResultOwner>().HasIndex(e => e.ParticipantId);
        modelBuilder.Entity<EvaluationResultOwner>().ToTable(t => t.HasCheckConstraint("CK_EvaluationResultOwner_ResultOwner_ExactlyOne", "CASE WHEN \"CompetitorId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"ParticipantId\" IS NULL THEN 0 ELSE 1 END = 1"));
        modelBuilder.Entity<EvaluationResultOwner>().HasIndex(e => e.EvaluationId).IsUnique();
        modelBuilder.Entity<EvaluationResultOwner>().HasIndex(e => new { e.EvaluationId, e.CompetitorId }).IsUnique().HasFilter("\"CompetitorId\" IS NOT NULL");
        modelBuilder.Entity<EvaluationResultOwner>().HasIndex(e => new { e.EvaluationId, e.ParticipantId }).IsUnique().HasFilter("\"ParticipantId\" IS NOT NULL");
        modelBuilder.Entity<CompetitorMember>().HasOne(e => e.Competitor).WithMany(e => e.CompetitorMembers).HasForeignKey(e => e.CompetitorId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitorMember>().HasIndex(e => e.CompetitorId);
        modelBuilder.Entity<CompetitorMember>().HasOne(e => e.Person).WithMany(e => e.CompetitorMembers).HasForeignKey(e => e.PersonId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompetitorMember>().HasIndex(e => e.PersonId);
        modelBuilder.Entity<CompetitorMember>().HasIndex(e => new { e.CompetitorId, e.PersonId }).IsUnique();
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Idspace).WithMany(e => e.MapsTos).HasForeignKey(e => e.IdspaceId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.IdspaceId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Competition).WithMany(e => e.MapsTos).HasForeignKey(e => e.CompetitionId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.CompetitionCall).WithMany(e => e.MapsTos).HasForeignKey(e => e.CompetitionCallId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.CompetitionCallId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Country).WithMany(e => e.MapsTos).HasForeignKey(e => e.CountryId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.CountryId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Event).WithMany(e => e.MapsTos).HasForeignKey(e => e.EventId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.EventId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Organization).WithMany(e => e.MapsTos).HasForeignKey(e => e.OrganizationId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.OrganizationId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Participant).WithMany(e => e.MapsTos).HasForeignKey(e => e.ParticipantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.ParticipantId);
        modelBuilder.Entity<MapsTo>().HasOne(e => e.Person).WithMany(e => e.MapsTos).HasForeignKey(e => e.PersonId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MapsTo>().HasIndex(e => e.PersonId);
        modelBuilder.Entity<MapsTo>().ToTable(t => t.HasCheckConstraint("CK_MapsTo_WithExternal_ExactlyOne", "CASE WHEN \"CompetitionId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CompetitionCallId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CountryId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"EventId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"OrganizationId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"ParticipantId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"PersonId\" IS NULL THEN 0 ELSE 1 END = 1"));
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.CompetitionCallId }).IsUnique().HasFilter("\"CompetitionCallId\" IS NOT NULL");
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.CompetitionId }).IsUnique().HasFilter("\"CompetitionId\" IS NOT NULL");
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.CountryId }).IsUnique().HasFilter("\"CountryId\" IS NOT NULL");
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.EventId }).IsUnique().HasFilter("\"EventId\" IS NOT NULL");
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.OrganizationId }).IsUnique().HasFilter("\"OrganizationId\" IS NOT NULL");
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.ParticipantId }).IsUnique().HasFilter("\"ParticipantId\" IS NOT NULL");
        modelBuilder.Entity<MapsTo>().HasIndex(e => new { e.IdspaceId, e.PersonId }).IsUnique().HasFilter("\"PersonId\" IS NOT NULL");
        modelBuilder.Entity<AccountRoleScope>().HasOne(e => e.Role).WithMany(e => e.AccountRoleScopes).HasForeignKey(e => e.RoleId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => e.RoleId);
        modelBuilder.Entity<AccountRoleScope>().HasOne(e => e.Competition).WithMany(e => e.AccountRoleScopes).HasForeignKey(e => e.CompetitionId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => e.CompetitionId);
        modelBuilder.Entity<AccountRoleScope>().HasOne(e => e.Event).WithMany(e => e.AccountRoleScopes).HasForeignKey(e => e.EventId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => e.EventId);
        modelBuilder.Entity<AccountRoleScope>().HasOne(e => e.Floor).WithMany(e => e.AccountRoleScopes).HasForeignKey(e => e.FloorId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => e.FloorId);
        modelBuilder.Entity<AccountRoleScope>().HasOne(e => e.Organization).WithMany(e => e.AccountRoleScopes).HasForeignKey(e => e.OrganizationId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => e.OrganizationId);
        modelBuilder.Entity<AccountRoleScope>().HasOne(e => e.Account).WithMany(e => e.AccountRoleScopes).HasForeignKey(e => e.AccountId).IsRequired(true).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => e.AccountId);
        modelBuilder.Entity<AccountRoleScope>().ToTable(t => t.HasCheckConstraint("CK_AccountRoleScope_Scope_ExactlyOne", "CASE WHEN \"CompetitionId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"EventId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"FloorId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"OrganizationId\" IS NULL THEN 0 ELSE 1 END = 1"));
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => new { e.RoleId, e.CompetitionId, e.AccountId }).IsUnique().HasFilter("\"CompetitionId\" IS NOT NULL");
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => new { e.RoleId, e.EventId, e.AccountId }).IsUnique().HasFilter("\"EventId\" IS NOT NULL");
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => new { e.RoleId, e.FloorId, e.AccountId }).IsUnique().HasFilter("\"FloorId\" IS NOT NULL");
        modelBuilder.Entity<AccountRoleScope>().HasIndex(e => new { e.RoleId, e.OrganizationId, e.AccountId }).IsUnique().HasFilter("\"OrganizationId\" IS NOT NULL");
        // TODO: Cardinality 1..* for Competition.CompetitionCalls requires existence validation.
        // TODO: Cardinality 1..* for ParticipantDefinition.ContestValue requires existence validation.
    }
}
