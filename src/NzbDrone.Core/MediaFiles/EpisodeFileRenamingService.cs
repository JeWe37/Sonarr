using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public interface IRenameEpisodeFiles
    {
        EpisodeFile MoveEpisodeFile(EpisodeFile episodeFile, Series series);
        EpisodeFile MoveEpisodeFile(EpisodeFile episodeFile, Series series, List<Episode> episodes);
    }

    public class EpisodeFileRenamingService : EpisodeFileTransferringService, IRenameEpisodeFiles
    {
        private readonly IEpisodeService _episodeService;
        private readonly IDiskTransferService _diskTransferService;
        private readonly Logger _logger;

        public EpisodeFileRenamingService(IEpisodeService episodeService,
                                IUpdateEpisodeFileService updateEpisodeFileService,
                                IBuildFileNames buildFileNames,
                                IDiskTransferService diskTransferService,
                                IDiskProvider diskProvider,
                                IMediaFileAttributeService mediaFileAttributeService,
                                IEventAggregator eventAggregator,
                                Logger logger)
            : base(updateEpisodeFileService, buildFileNames, diskProvider, mediaFileAttributeService, eventAggregator, logger)
        {
            _episodeService = episodeService;
            _diskTransferService = diskTransferService;
            _logger = logger;
        }

        protected override void ExecuteTransfer(string episodeFilePath, string destinationFilePath, TransferMode mode, ScriptImportDecisionInfo scriptImportDecisionInfo)
        {
            _diskTransferService.TransferFile(episodeFilePath, destinationFilePath, mode);
        }

        public EpisodeFile MoveEpisodeFile(EpisodeFile episodeFile, Series series)
        {
            var episodes = _episodeService.GetEpisodesByFileId(episodeFile.Id);
            return MoveEpisodeFile(episodeFile, series, episodes);
        }

        public EpisodeFile MoveEpisodeFile(EpisodeFile episodeFile, Series series, List<Episode> episodes)
        {
            var filePath = _buildFileNames.BuildFilePath(episodes, series, episodeFile, Path.GetExtension(episodeFile.RelativePath));

            EnsureEpisodeFolder(episodeFile, series, episodes.Select(v => v.SeasonNumber).First(), filePath);

            _logger.Debug("Renaming episode file: {0} to {1}", episodeFile, filePath);

            return TransferFile(episodeFile, series, episodes, filePath, TransferMode.Move);
        }
    }
}
