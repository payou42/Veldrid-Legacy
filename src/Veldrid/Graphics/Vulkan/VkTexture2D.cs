﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vulkan;
using static Veldrid.Graphics.Vulkan.VulkanUtil;
using static Vulkan.VulkanNative;

namespace Veldrid.Graphics.Vulkan
{
    public unsafe class VkTexture2D : VkDeviceTexture, DeviceTexture2D
    {
        private readonly VkDevice _device;
        private readonly VkPhysicalDevice _physicalDevice;
        private readonly VkRenderContext _rc;

        private VkImage _image;
        private VkDeviceMemory _memory;
        private PixelFormat _veldridFormat;
        private DeviceTextureCreateOptions _createOptions;
        private VkImageLayout _imageLayout;
        private int _width;
        private int _height;

        public VkTexture2D(
            VkDevice device,
            VkPhysicalDevice physicalDevice,
            VkRenderContext rc,
            int mipLevels,
            int width,
            int height,
            PixelFormat veldridFormat,
            DeviceTextureCreateOptions createOptions)
        {
            _device = device;
            _physicalDevice = physicalDevice;
            _rc = rc;

            MipLevels = mipLevels;
            _width = width;
            _height = height;
            _createOptions = createOptions;
            if (createOptions == DeviceTextureCreateOptions.DepthStencil)
            {
                Format = VkFormat.D16Unorm;
            }
            else
            {
                Format = VkFormats.VeldridToVkPixelFormat(veldridFormat);
            }

            _veldridFormat = veldridFormat;

            VkImageCreateInfo imageCI = VkImageCreateInfo.New();
            imageCI.mipLevels = (uint)mipLevels;
            imageCI.arrayLayers = 1;
            imageCI.imageType = VkImageType.Image2D;
            imageCI.extent.width = (uint)width;
            imageCI.extent.height = (uint)height;
            imageCI.extent.depth = 1;
            imageCI.initialLayout = VkImageLayout.Preinitialized; // TODO: Use proper VkImageLayout values and transitions.
            imageCI.usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
            if (createOptions == DeviceTextureCreateOptions.RenderTarget)
            {
                imageCI.usage |= VkImageUsageFlags.ColorAttachment;
            }
            else if (createOptions == DeviceTextureCreateOptions.DepthStencil)
            {
                imageCI.usage |= VkImageUsageFlags.DepthStencilAttachment;
            }
            imageCI.tiling = createOptions == DeviceTextureCreateOptions.DepthStencil ? VkImageTiling.Optimal : VkImageTiling.Optimal;
            imageCI.format = Format;

            imageCI.samples = VkSampleCountFlags.Count1;

            VkResult result = vkCreateImage(device, ref imageCI, null, out _image);
            CheckResult(result);

            vkGetImageMemoryRequirements(_device, _image, out VkMemoryRequirements memoryRequirements);

            VkMemoryAllocateInfo memoryAI = VkMemoryAllocateInfo.New();
            memoryAI.allocationSize = memoryRequirements.size;
            memoryAI.memoryTypeIndex = FindMemoryType(
                _physicalDevice,
                memoryRequirements.memoryTypeBits,
                VkMemoryPropertyFlags.DeviceLocal);
            vkAllocateMemory(_device, ref memoryAI, null, out _memory);
            vkBindImageMemory(_device, _image, _memory, 0);
        }

        public VkTexture2D(
            VkDevice device,
            int mipLevels,
            int width,
            int height,
            VkFormat vkFormat,
            VkImage existingImage)
        {
            _device = device;
            MipLevels = mipLevels;
            _width = width;
            _height = height;
            Format = vkFormat;
            _veldridFormat = VkFormats.VkToVeldridPixelFormat(vkFormat);
            _image = existingImage;
        }

        public override int Width => _width;

        public override int Height => _height;

        public override int MipLevels { get; }

        public override VkFormat Format { get; }

        public override VkImage DeviceImage => _image;

        public override DeviceTextureCreateOptions CreateOptions => _createOptions;

        public void GetTextureData(int mipLevel, IntPtr destination, int storageSizeInBytes)
        {
            throw new NotImplementedException();
        }

        public void GetTextureData<T>(int mipLevel, T[] destination) where T : struct
        {
            throw new NotImplementedException();
        }

        public void SetTextureData(int mipLevel, int x, int y, int width, int height, IntPtr data, int dataSizeInBytes)
        {
            if (x != 0 || y != 0)
            {
                throw new NotImplementedException();
            }

            // First, create a staging texture.
            CreateImage(
                _device,
                _physicalDevice,
                (uint)width,
                (uint)height,
                1,
                Format,
                VkImageTiling.Linear,
                VkImageUsageFlags.TransferSrc,
                VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
                out VkImage stagingImage,
                out VkDeviceMemory stagingMemory);

            VkImageSubresource subresource = new VkImageSubresource();
            subresource.aspectMask = VkImageAspectFlags.Color;
            subresource.mipLevel = 0;
            subresource.arrayLayer = 0;
            vkGetImageSubresourceLayout(_device, stagingImage, ref subresource, out VkSubresourceLayout stagingLayout);
            ulong rowPitch = stagingLayout.rowPitch;

            void* mappedPtr;
            VkResult result = vkMapMemory(_device, stagingMemory, 0, stagingLayout.size, 0, &mappedPtr);
            CheckResult(result);

            if (rowPitch == (ulong)width)
            {
                Buffer.MemoryCopy(data.ToPointer(), mappedPtr, dataSizeInBytes, dataSizeInBytes);
            }
            else
            {
                int pixelSizeInBytes = FormatHelpers.GetPixelSizeInBytes(_veldridFormat);
                for (uint yy = 0; yy < height; yy++)
                {
                    byte* dstRowStart = ((byte*)mappedPtr) + (rowPitch * yy);
                    byte* srcRowStart = ((byte*)data.ToPointer()) + (width * yy * pixelSizeInBytes);
                    Unsafe.CopyBlock(dstRowStart, srcRowStart, (uint)(width * pixelSizeInBytes));
                }
            }

            vkUnmapMemory(_device, stagingMemory);

            TransitionImageLayout(stagingImage, 1, VkImageLayout.Preinitialized, VkImageLayout.TransferSrcOptimal);
            TransitionImageLayout(_image, (uint)MipLevels, _imageLayout, VkImageLayout.TransferDstOptimal);
            CopyImage(stagingImage, 0, _image, (uint)mipLevel, (uint)width, (uint)height);
            TransitionImageLayout(_image, (uint)MipLevels, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal);
            _imageLayout = VkImageLayout.ShaderReadOnlyOptimal;

            vkDestroyImage(_device, stagingImage, null);
        }

        public override void Dispose()
        {
            vkDestroyImage(_device, _image, null);
            if (_memory != VkDeviceMemory.Null)
            {
                vkFreeMemory(_device, _memory, null);
            }
        }

        protected void TransitionImageLayout(VkImage image, uint mipLevels, VkImageLayout oldLayout, VkImageLayout newLayout)
        {
            VkCommandBuffer cb = BeginOneTimeCommands();

            VkImageMemoryBarrier barrier = VkImageMemoryBarrier.New();
            barrier.oldLayout = oldLayout;
            barrier.newLayout = newLayout;
            barrier.srcQueueFamilyIndex = QueueFamilyIgnored;
            barrier.dstQueueFamilyIndex = QueueFamilyIgnored;
            barrier.image = image;
            barrier.subresourceRange.aspectMask = VkImageAspectFlags.Color;
            barrier.subresourceRange.baseMipLevel = 0;
            barrier.subresourceRange.levelCount = mipLevels;
            barrier.subresourceRange.baseArrayLayer = 0;
            barrier.subresourceRange.layerCount = 1;

            vkCmdPipelineBarrier(
                cb,
                VkPipelineStageFlags.TopOfPipe,
                VkPipelineStageFlags.TopOfPipe,
                VkDependencyFlags.None,
                0, null,
                0, null,
                1, &barrier);

            EndOneTimeCommands(cb);
        }

        protected void CopyImage(VkImage srcImage, uint srcMipLevel, VkImage dstImage, uint dstMipLevel, uint width, uint height)
        {
            VkImageSubresourceLayers srcSubresource = new VkImageSubresourceLayers();
            srcSubresource.mipLevel = srcMipLevel;
            srcSubresource.layerCount = 1;
            srcSubresource.aspectMask = VkImageAspectFlags.Color;
            srcSubresource.baseArrayLayer = 0;

            VkImageSubresourceLayers dstSubresource = new VkImageSubresourceLayers();
            dstSubresource.mipLevel = dstMipLevel;
            dstSubresource.layerCount = 1;
            dstSubresource.aspectMask = VkImageAspectFlags.Color;
            dstSubresource.baseArrayLayer = 0;

            VkImageCopy region = new VkImageCopy();
            region.dstSubresource = dstSubresource;
            region.srcSubresource = srcSubresource;
            region.extent.width = width;
            region.extent.height = height;
            region.extent.depth = 1;

            VkCommandBuffer copyCmd = BeginOneTimeCommands();
            vkCmdCopyImage(copyCmd, srcImage, VkImageLayout.TransferSrcOptimal, dstImage, VkImageLayout.TransferDstOptimal, 1, ref region);
            EndOneTimeCommands(copyCmd);
        }

        protected VkCommandBuffer BeginOneTimeCommands()
        {
            VkCommandBufferAllocateInfo allocInfo = VkCommandBufferAllocateInfo.New();
            allocInfo.commandBufferCount = 1;
            allocInfo.commandPool = _rc.GraphicsCommandPool;
            allocInfo.level = VkCommandBufferLevel.Primary;

            vkAllocateCommandBuffers(_device, ref allocInfo, out VkCommandBuffer cb);

            VkCommandBufferBeginInfo beginInfo = VkCommandBufferBeginInfo.New();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmit;

            vkBeginCommandBuffer(cb, ref beginInfo);

            return cb;
        }

        protected void EndOneTimeCommands(VkCommandBuffer cb)
        {
            vkEndCommandBuffer(cb);

            VkSubmitInfo submitInfo = VkSubmitInfo.New();
            submitInfo.commandBufferCount = 1;
            submitInfo.pCommandBuffers = &cb;

            vkQueueSubmit(_rc.GraphicsQueue, 1, ref submitInfo, VkFence.Null);
            vkQueueWaitIdle(_rc.GraphicsQueue);

            vkFreeCommandBuffers(_device, _rc.GraphicsCommandPool, 1, ref cb);
        }
    }
}
