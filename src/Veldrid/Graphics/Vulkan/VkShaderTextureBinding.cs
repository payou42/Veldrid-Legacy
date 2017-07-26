﻿using Vulkan;
using static Veldrid.Graphics.Vulkan.VulkanUtil;
using static Vulkan.VulkanNative;

namespace Veldrid.Graphics.Vulkan
{
    public unsafe class VkShaderTextureBinding : ShaderTextureBinding
    {
        private readonly VkDevice _device;

        public VkShaderTextureBinding(VkDevice device, VkDeviceTexture deviceTexture)
        {
            _device = device;
            BoundTexture = deviceTexture;
            VkImageViewCreateInfo imageViewCI = VkImageViewCreateInfo.New();
            imageViewCI.format = deviceTexture.Format;
            imageViewCI.image = deviceTexture.DeviceImage;
            imageViewCI.viewType = VkImageViewType.Image2D;
            imageViewCI.subresourceRange.aspectMask = deviceTexture.CreateOptions == DeviceTextureCreateOptions.DepthStencil ? VkImageAspectFlags.Depth : VkImageAspectFlags.Color;
            imageViewCI.subresourceRange.layerCount = 1;
            imageViewCI.subresourceRange.levelCount = (uint)deviceTexture.MipLevels;

            VkResult result = vkCreateImageView(_device, ref imageViewCI, null, out VkImageView imageView);
            CheckResult(result);
            ImageView = imageView;
        }

        public VkImageView ImageView { get; }
        public VkDeviceTexture BoundTexture { get; }
        public VkImageLayout ImageLayout { get; internal set; }

        DeviceTexture ShaderTextureBinding.BoundTexture => BoundTexture;

        public void Dispose()
        {
            vkDestroyImageView(_device, ImageView, null);
        }
    }
}
